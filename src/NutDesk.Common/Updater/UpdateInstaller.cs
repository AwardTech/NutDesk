using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace NutDesk.Common.Updater
{
    /// <summary>
    /// The file side of an update: the download, its SHA-256 verification, the extraction into a staging folder,
    /// the launch of the in-place install, and the clean-up of what earlier updates left behind.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Everything lives under <see cref="AppGlobals.UpdatesDirectory"/>, one folder per release tag:
    /// Updates\&lt;tag&gt;\&lt;zip name&gt; (the verified download), Updates\&lt;tag&gt;\staging (the unpacked ZIP) and
    /// Updates\&lt;tag&gt;\backup (written by the apply mode: the files it replaced). A tag folder is removed by the
    /// first start of a version at least as new as the tag (<see cref="CleanupUpdatesDirectory(Version)"/>).
    /// </para>
    /// <para>
    /// The async methods do their work on the thread pool and never resume on the caller's context; the UI calls
    /// them through <see cref="UpdateUtil.PrepareUpdateAsync"/>, which does. They throw
    /// <see cref="UpdateException"/> on failure and <see cref="OperationCanceledException"/> when cancelled.
    /// </para>
    /// </remarks>
    public static class UpdateInstaller
    {
        /// <summary>The executable a package must contain, and the one the apply mode restarts.</summary>
        public const string ExecutableName = "NutDesk.exe";

        /// <summary>The subfolder of Updates\&lt;tag&gt; the ZIP is unpacked into.</summary>
        public const string StagingFolderName = "staging";

        /// <summary>The subfolder of Updates\&lt;tag&gt; the apply mode saves the replaced files into.</summary>
        public const string BackupFolderName = "backup";

        /// <summary>Appended to the package name to find its checksum asset.</summary>
        public const string ChecksumExtension = ".sha256";

        private const string PARTIAL_EXTENSION = ".part";

        // The checksum file is one line; anything much bigger is not what it claims to be.
        private const int MAX_CHECKSUM_BYTES = 64 * 1024;

        // A NutDesk package unpacks to a few megabytes. The cap only stops a malformed or hostile archive from
        // filling the disk.
        private const long MAX_EXTRACTED_BYTES = 512L * 1024 * 1024;

        private const int BUFFER_SIZE = 81920;

        // How often the download reports progress.
        private static readonly TimeSpan PROGRESS_INTERVAL = TimeSpan.FromMilliseconds(250);

        // The download is abandoned when no data arrives for this long. HttpClient's own timeout stops at the
        // response headers; the body is read from a stream that would otherwise wait forever.
        private static readonly TimeSpan STALL_TIMEOUT = TimeSpan.FromSeconds(60);

        private static readonly Lazy<HttpClient> http = new Lazy<HttpClient>(CreateHttpClient);

        #region Download

        /// <summary>
        /// The folder of one release under <see cref="AppGlobals.UpdatesDirectory"/>, named after its tag.
        /// </summary>
        public static string GetReleaseDirectory(UpdateCheckResult update)
        {
            if (update == null)
            {
                throw new ArgumentNullException(nameof(update));
            }

            var name = update.VersionKey.Length > 0 ? update.VersionKey : update.VersionText;
            return Path.Combine(AppGlobals.UpdatesDirectory, SafeName(name, "release"));
        }

        /// <summary>
        /// Reads the SHA-256 the release publishes for its package, from the "&lt;zip name&gt;.sha256" asset
        /// (content: "&lt;hex&gt;  &lt;file name&gt;"). Returned in lower case.
        /// </summary>
        /// <exception cref="UpdateException">
        /// <see cref="UpdateFailure.ChecksumMissing"/> when the asset is absent or holds no usable hash for the
        /// package; a network kind when it could not be downloaded.
        /// </exception>
        public static async Task<string> FetchExpectedHashAsync(UpdateCheckResult update, CancellationToken cancellationToken)
        {
            RequirePackage(update);

            var asset = update.ChecksumAsset;
            if (asset == null)
            {
                throw new UpdateException(UpdateFailure.ChecksumMissing,
                    $"Release {update.VersionKey} publishes no {update.Package.Name}{ChecksumExtension}.");
            }

            string text;
            try
            {
                using (var response = await http.Value.GetAsync(new Uri(asset.BrowserDownloadUrl), HttpCompletionOption.ResponseHeadersRead,
                                                                cancellationToken).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        throw new UpdateException(UpdateFailure.ServerError,
                            $"Downloading {asset.Name} returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).");
                    }

                    using (var body = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    {
                        text = await ReadLimitedTextAsync(body, MAX_CHECKSUM_BYTES, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex) when (!(ex is UpdateException) && !cancellationToken.IsCancellationRequested)
            {
                // Nothing is written to disk here, so an IOException or a disposed stream is the connection breaking,
                // not a disk problem: reported as FileSystem it would tell the user the update files could not be written.
                var failure = UpdateException.Classify(ex);
                if (failure == UpdateFailure.FileSystem || failure == UpdateFailure.Unexpected || ex is ObjectDisposedException)
                {
                    failure = UpdateFailure.Network;
                }

                throw new UpdateException(failure, $"Could not download {asset.Name}: {UpdateException.InnermostMessage(ex)}", ex);
            }

            var hash = ParseChecksum(text, update.Package.Name);
            if (hash == null)
            {
                throw new UpdateException(UpdateFailure.ChecksumMissing,
                    $"{asset.Name} holds no SHA-256 for {update.Package.Name}.");
            }

            AppGlobals.LogFile.LogTracing($"Published SHA-256 of {update.Package.Name}: {hash}.", LogLvl.LOG_DEBUG, null);
            return hash;
        }

        /// <summary>
        /// The hash for <paramref name="fileName"/> in the text of a checksum file, or null. Accepts the
        /// "&lt;hex&gt;  &lt;name&gt;" lines sha256sum writes (with or without the binary-mode '*'), and a bare hash; a
        /// line naming another file does not count.
        /// </summary>
        public static string ParseChecksum(string text, string fileName)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            string bareHash = null;
            foreach (var rawLine in text.TrimStart('﻿').Split('\n'))
            {
                var match = Regex.Match(rawLine.Trim(), @"^([0-9a-fA-F]{64})(?:\s+\*?(.+))?$");
                if (!match.Success)
                {
                    continue;
                }

                var hash = match.Groups[1].Value.ToLowerInvariant();
                if (!match.Groups[2].Success)
                {
                    bareHash = bareHash ?? hash;
                    continue;
                }

                var listedName = match.Groups[2].Value.Trim();
                if (string.Equals(Path.GetFileName(listedName.Replace('/', '\\')), fileName, StringComparison.OrdinalIgnoreCase))
                {
                    return hash;
                }
            }

            return bareHash;
        }

        /// <summary>
        /// Downloads the release's package into <see cref="GetReleaseDirectory"/> through a ".part" file, and
        /// returns the path of the finished file. A file already there is reused only when its SHA-256 equals
        /// <paramref name="expectedHash"/>; otherwise it is replaced.
        /// </summary>
        /// <remarks>
        /// The result is not verified here - <see cref="VerifyAsync"/> does that - except that a download shorter or
        /// longer than the size GitHub announced is refused as a broken transfer.
        /// </remarks>
        public static async Task<string> DownloadAsync(UpdateCheckResult update, string expectedHash,
                                                       IProgress<UpdateProgress> progress, CancellationToken cancellationToken)
        {
            RequirePackage(update);

            var asset = update.Package;
            var folder = GetReleaseDirectory(update);
            var target = Path.Combine(folder, SafeName(Path.GetFileName(asset.Name), "NutDesk-update.zip"));
            var partial = target + PARTIAL_EXTENSION;
            long announced = asset.Size;

            try
            {
                Directory.CreateDirectory(folder);
            }
            catch (Exception ex)
            {
                throw new UpdateException(UpdateFailure.FileSystem, $"Could not create {folder}: {ex.Message}", ex);
            }

            if (File.Exists(target))
            {
                string existingHash = null;
                if (!string.IsNullOrEmpty(expectedHash))
                {
                    try
                    {
                        existingHash = await ComputeSha256Async(target, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (!(ex is OperationCanceledException))
                    {
                        AppGlobals.LogFile.LogTracing($"Could not read the earlier download {target}: {ex.Message}", LogLvl.LOG_DEBUG, null);
                    }
                }

                if (existingHash != null && string.Equals(existingHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                {
                    AppGlobals.LogFile.LogTracing($"Reusing {target}, already downloaded and matching the published SHA-256.",
                                                  LogLvl.LOG_NOTICE, null);
                    var size = new FileInfo(target).Length;
                    progress?.Report(new UpdateProgress(UpdateStage.Downloading, size, size));
                    return target;
                }

                AppGlobals.LogFile.LogTracing($"Discarding {target}: it does not match the published SHA-256.", LogLvl.LOG_NOTICE, null);
                DeleteFileOrThrow(target);
            }

            AppGlobals.LogFile.LogTracing($"Downloading {asset.BrowserDownloadUrl} to {partial}.", LogLvl.LOG_NOTICE, null);

            // Which side a failure comes from decides how it is reported: an IOException while reading the network
            // is a broken transfer, the same exception while writing the file is a disk problem.
            bool writing = false;
            long received = 0;

            try
            {
                using (var stall = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    stall.CancelAfter(STALL_TIMEOUT);

                    using (var response = await http.Value.GetAsync(new Uri(asset.BrowserDownloadUrl), HttpCompletionOption.ResponseHeadersRead,
                                                                    stall.Token).ConfigureAwait(false))
                    {
                        if (!response.IsSuccessStatusCode)
                        {
                            throw new UpdateException(UpdateFailure.ServerError,
                                $"Downloading {asset.Name} returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).");
                        }

                        if (announced <= 0)
                        {
                            announced = response.Content.Headers.ContentLength ?? 0;
                        }

                        writing = true;
                        using (var file = new FileStream(partial, System.IO.FileMode.Create, FileAccess.Write, FileShare.None,
                                                         BUFFER_SIZE, useAsync: true))
                        {
                            writing = false;

                            using (var body = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                            // On the .NET Framework a read already waiting on the response stream ignores its token,
                            // so a cancellation or a stall closes the response under it instead.
                            using (stall.Token.Register(() => CloseQuietly(response)))
                            {
                                var buffer = new byte[BUFFER_SIZE];
                                var nextReport = DateTime.UtcNow;
                                int read;

                                while ((read = await body.ReadAsync(buffer, 0, buffer.Length, stall.Token).ConfigureAwait(false)) > 0)
                                {
                                    stall.CancelAfter(STALL_TIMEOUT);

                                    writing = true;
                                    await file.WriteAsync(buffer, 0, read, cancellationToken).ConfigureAwait(false);
                                    writing = false;

                                    received += read;
                                    if (DateTime.UtcNow >= nextReport)
                                    {
                                        progress?.Report(new UpdateProgress(UpdateStage.Downloading, received, Math.Max(announced, received)));
                                        nextReport = DateTime.UtcNow + PROGRESS_INTERVAL;
                                    }
                                }

                                cancellationToken.ThrowIfCancellationRequested();
                                if (stall.IsCancellationRequested)
                                {
                                    throw new UpdateException(UpdateFailure.Network,
                                        $"No data received for {STALL_TIMEOUT.TotalSeconds} seconds.");
                                }
                            }
                        }
                    }

                    // The last chunk rarely lands on a progress tick; let the bar reach the end.
                    progress?.Report(new UpdateProgress(UpdateStage.Downloading, received, Math.Max(announced, received)));

                    if (announced > 0 && received != announced)
                    {
                        throw new UpdateException(UpdateFailure.Network,
                            $"Download of {asset.Name} incomplete: {received} of {announced} bytes.");
                    }
                }
            }
            catch (Exception ex)
            {
                TryDeleteFile(partial);

                // Whatever broke the transfer on the way out, a cancelled download is reported as cancelled.
                cancellationToken.ThrowIfCancellationRequested();

                if (ex is UpdateException)
                {
                    throw;
                }

                var failure = writing ? UpdateFailure.FileSystem : UpdateException.Classify(ex);
                if (!writing && (failure == UpdateFailure.FileSystem || failure == UpdateFailure.Unexpected ||
                                 ex is ObjectDisposedException))
                {
                    // An IOException or a disposed stream while reading is the connection breaking or stalling.
                    failure = UpdateFailure.Network;
                }

                throw new UpdateException(failure, $"Download of {asset.Name} failed after {received} bytes: " +
                                                   UpdateException.InnermostMessage(ex), ex);
            }

            try
            {
                File.Move(partial, target);
            }
            catch (Exception ex)
            {
                TryDeleteFile(partial);
                throw new UpdateException(UpdateFailure.FileSystem, $"Could not rename {partial}: {ex.Message}", ex);
            }

            AppGlobals.LogFile.LogTracing($"Update downloaded to {target} ({received} bytes).", LogLvl.LOG_NOTICE, null);
            return target;
        }

        /// <summary>
        /// Checks the SHA-256 of a downloaded package. A file that does not match is deleted, so it can never be
        /// mistaken for a good download later.
        /// </summary>
        /// <exception cref="UpdateException">
        /// <see cref="UpdateFailure.ChecksumMissing"/> without an expected hash,
        /// <see cref="UpdateFailure.ChecksumMismatch"/> when the hashes differ.
        /// </exception>
        public static async Task VerifyAsync(string archivePath, string expectedHash, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(expectedHash))
            {
                throw new UpdateException(UpdateFailure.ChecksumMissing, "No published SHA-256 to verify " + archivePath + " against.");
            }

            string actual;
            try
            {
                actual = await ComputeSha256Async(archivePath, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                throw new UpdateException(UpdateFailure.FileSystem, $"Could not read {archivePath}: {ex.Message}", ex);
            }

            if (!string.Equals(actual, expectedHash.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                TryDeleteFile(archivePath);
                throw new UpdateException(UpdateFailure.ChecksumMismatch,
                    $"SHA-256 of {Path.GetFileName(archivePath)} is {actual}, the release publishes {expectedHash}. The file has been deleted.");
            }

            AppGlobals.LogFile.LogTracing($"{Path.GetFileName(archivePath)} matches the published SHA-256.", LogLvl.LOG_NOTICE, null);
        }

        /// <summary>
        /// The SHA-256 of a file, in lower-case hex, computed on the thread pool.
        /// </summary>
        public static Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                using (var sha = SHA256.Create())
                using (var stream = new FileStream(path, System.IO.FileMode.Open, FileAccess.Read, FileShare.Read, BUFFER_SIZE))
                {
                    var buffer = new byte[BUFFER_SIZE];
                    int read;
                    while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        sha.TransformBlock(buffer, 0, read, null, 0);
                    }
                    sha.TransformFinalBlock(buffer, 0, 0);

                    var hex = new StringBuilder(64);
                    foreach (var b in sha.Hash)
                    {
                        hex.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                    }
                    return hex.ToString();
                }
            }, cancellationToken);
        }

        #endregion

        #region Extraction

        /// <summary>
        /// Unpacks a verified package into Updates\&lt;tag&gt;\staging (emptied first), finds the folder holding
        /// NutDesk.exe - the ZIP's versioned NutDesk-X.Y.Z folder, or the root of a flat ZIP - and checks that this
        /// NutDesk.exe carries the release's version.
        /// </summary>
        /// <exception cref="UpdateException">
        /// <see cref="UpdateFailure.InvalidPackage"/> for a damaged ZIP, an entry that would land outside the staging
        /// folder (zip slip) or a package without NutDesk.exe; <see cref="UpdateFailure.VersionMismatch"/>;
        /// <see cref="UpdateFailure.FileSystem"/>.
        /// </exception>
        public static Task<PreparedUpdate> ExtractAsync(UpdateCheckResult update, string archivePath, CancellationToken cancellationToken)
        {
            RequirePackage(update);
            return Task.Run(() => Extract(update, archivePath, cancellationToken), cancellationToken);
        }

        private static PreparedUpdate Extract(UpdateCheckResult update, string archivePath, CancellationToken cancellationToken)
        {
            var staging = Path.Combine(GetReleaseDirectory(update), StagingFolderName);

            try
            {
                if (Directory.Exists(staging) && !TryDeleteDirectory(staging))
                {
                    // Something (an Explorer window, a scanner) held a file; one more try before giving up.
                    Thread.Sleep(500);
                    if (!TryDeleteDirectory(staging))
                    {
                        throw new IOException("The previous staging folder could not be emptied.");
                    }
                }
                Directory.CreateDirectory(staging);
            }
            catch (Exception ex)
            {
                throw new UpdateException(UpdateFailure.FileSystem, $"Could not prepare {staging}: {ex.Message}", ex);
            }

            try
            {
                UnpackSafely(archivePath, staging, cancellationToken);
            }
            catch (Exception ex)
            {
                // Half an extraction must not look like a staged update.
                TryDeleteDirectory(staging);

                if (ex is UpdateException || ex is OperationCanceledException)
                {
                    throw;
                }

                var failure = ex is InvalidDataException ? UpdateFailure.InvalidPackage : UpdateException.Classify(ex);
                throw new UpdateException(failure == UpdateFailure.Unexpected ? UpdateFailure.InvalidPackage : failure,
                                          $"Could not unpack {Path.GetFileName(archivePath)}: {ex.Message}", ex);
            }

            var payload = FindPayloadDirectory(staging);
            if (payload == null)
            {
                TryDeleteDirectory(staging);
                throw new UpdateException(UpdateFailure.InvalidPackage, $"{Path.GetFileName(archivePath)} contains no {ExecutableName}.");
            }

            var executable = Path.Combine(payload, ExecutableName);
            Version stagedVersion;
            try
            {
                stagedVersion = AssemblyName.GetAssemblyName(executable).Version;
            }
            catch (Exception ex)
            {
                TryDeleteDirectory(staging);
                throw new UpdateException(UpdateFailure.InvalidPackage, $"{executable} is not a usable assembly: {ex.Message}", ex);
            }

            if (stagedVersion == null || UpdateUtil.Normalize(stagedVersion) != update.Version)
            {
                TryDeleteDirectory(staging);
                throw new UpdateException(UpdateFailure.VersionMismatch,
                    $"The package's {ExecutableName} is version {stagedVersion}, the release announces {update.Version}.");
            }

            AppGlobals.LogFile.LogTracing($"Package unpacked to {staging}; {executable} is version {stagedVersion}.", LogLvl.LOG_NOTICE, null);
            return new PreparedUpdate(update, archivePath, staging, payload, executable);
        }

        /// <summary>
        /// Extracts every entry of the ZIP under <paramref name="root"/>, refusing the whole archive when any
        /// entry's full path would land outside it (zip slip: "..\" segments, absolute or UNC paths, drive letters).
        /// </summary>
        private static void UnpackSafely(string archivePath, string root, CancellationToken cancellationToken)
        {
            var rootPrefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            long total = 0;

            using (var archive = ZipFile.OpenRead(archivePath))
            {
                foreach (var entry in archive.Entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    string destination;
                    try
                    {
                        destination = Path.GetFullPath(Path.Combine(rootPrefix, entry.FullName));
                    }
                    catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException)
                    {
                        throw new UpdateException(UpdateFailure.InvalidPackage, $"Entry '{entry.FullName}' has an unusable path.", ex);
                    }

                    if (!destination.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new UpdateException(UpdateFailure.InvalidPackage,
                            $"Entry '{entry.FullName}' would be extracted outside the staging folder; the package is refused.");
                    }

                    // A folder entry: its name ends with the separator, so it has no file name.
                    if (entry.Name.Length == 0)
                    {
                        Directory.CreateDirectory(destination);
                        continue;
                    }

                    total += entry.Length;
                    if (total > MAX_EXTRACTED_BYTES)
                    {
                        throw new UpdateException(UpdateFailure.InvalidPackage, "The package unpacks to more than the allowed size.");
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    entry.ExtractToFile(destination, true);
                }
            }
        }

        /// <summary>
        /// The shallowest folder under <paramref name="staging"/> that holds NutDesk.exe, or null.
        /// </summary>
        private static string FindPayloadDirectory(string staging)
        {
            if (File.Exists(Path.Combine(staging, ExecutableName)))
            {
                return staging;
            }

            return Directory.GetFiles(staging, ExecutableName, SearchOption.AllDirectories)
                            .Where(file => string.Equals(Path.GetFileName(file), ExecutableName, StringComparison.OrdinalIgnoreCase))
                            .Select(Path.GetDirectoryName)
                            .OrderBy(dir => dir.Count(c => c == Path.DirectorySeparatorChar))
                            .ThenBy(dir => dir, StringComparer.OrdinalIgnoreCase)
                            .FirstOrDefault();
        }

        #endregion

        #region Installation

        /// <summary>
        /// True when an update can be installed in place into <paramref name="installDirectory"/>: the folder holds
        /// NutDesk.exe, is not read-only, is not inside the updates folder, and a file can be created in it. When
        /// false, offer the manual update (the staged folder opened in Explorer) instead.
        /// </summary>
        public static bool CanInstallInPlace(string installDirectory)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(installDirectory) || !Directory.Exists(installDirectory))
                {
                    return false;
                }

                var folder = Path.GetFullPath(installDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                var updates = Path.GetFullPath(AppGlobals.UpdatesDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (folder.StartsWith(updates, StringComparison.OrdinalIgnoreCase))
                {
                    // A copy started from a staging folder must not "update" the staging folder.
                    return false;
                }

                // The apply mode restarts NutDesk.exe; a copy renamed to something else is left to the user.
                var executable = Path.Combine(folder, ExecutableName);
                if (!File.Exists(executable) || (File.GetAttributes(executable) & FileAttributes.ReadOnly) != 0)
                {
                    return false;
                }

                using (File.Create(Path.Combine(folder, Path.GetRandomFileName()), 1, FileOptions.DeleteOnClose))
                {
                }

                return true;
            }
            catch (Exception ex)
            {
                AppGlobals.LogFile.LogTracing($"{installDirectory} cannot be updated in place: {ex.Message}", LogLvl.LOG_NOTICE, null);
                return false;
            }
        }

        /// <summary>
        /// Starts the staged executable in apply mode. See <see cref="UpdateUtil.StartInstall"/>.
        /// </summary>
        internal static void LaunchApplyMode(PreparedUpdate update, string installDirectory, bool persistData)
        {
            if (update == null)
            {
                throw new ArgumentNullException(nameof(update));
            }

            if (string.IsNullOrWhiteSpace(installDirectory))
            {
                throw new ArgumentException("No install folder.", nameof(installDirectory));
            }

            if (!File.Exists(update.ExecutablePath))
            {
                throw new UpdateException(UpdateFailure.LaunchFailed, $"The staged {update.ExecutablePath} no longer exists.");
            }

            int processId;
            using (var current = Process.GetCurrentProcess())
            {
                processId = current.Id;
            }

            var arguments = UpdateCommandLine.BuildApplyArguments(Path.GetFullPath(installDirectory), processId,
                                                                  UpdateUtil.RunningVersionText, persistData);
            // Through the shell: a process created directly would inherit every inheritable handle of this one, and
            // on the .NET Framework that includes the socket to the NUT server, which would then outlive this process.
            var startInfo = new ProcessStartInfo(update.ExecutablePath, arguments)
            {
                UseShellExecute = true,
                WorkingDirectory = update.PayloadDirectory,
            };

            try
            {
                using (var process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        throw new InvalidOperationException("No process was started.");
                    }
                }
            }
            catch (Exception ex)
            {
                throw new UpdateException(UpdateFailure.LaunchFailed, $"Could not start {update.ExecutablePath}: {ex.Message}", ex);
            }

            AppGlobals.LogFile.LogTracing($"Started {update.ExecutablePath} {arguments}; exiting so it can install.", LogLvl.LOG_NOTICE, null);
        }

        #endregion

        #region Clean-up

        /// <summary>
        /// Removes the release folders of versions up to <paramref name="runningVersion"/> (installed, or older)
        /// and any ".part" file left by an interrupted download. Tolerant: whatever is locked stays for the next
        /// start. Blocking; run it off the UI thread.
        /// </summary>
        public static void CleanupUpdatesDirectory(Version runningVersion)
        {
            var root = AppGlobals.UpdatesDirectory;
            try
            {
                if (runningVersion == null || !Directory.Exists(root))
                {
                    return;
                }

                var running = UpdateUtil.Normalize(runningVersion);
                foreach (var folder in Directory.GetDirectories(root))
                {
                    if (UpdateUtil.TryParseReleaseVersion(Path.GetFileName(folder), out Version version) && version <= running)
                    {
                        if (TryDeleteDirectory(folder))
                        {
                            AppGlobals.LogFile.LogTracing($"Removed the finished update folder {folder}.", LogLvl.LOG_DEBUG, null);
                        }
                    }
                }

                foreach (var partial in Directory.GetFiles(root, "*" + PARTIAL_EXTENSION, SearchOption.AllDirectories))
                {
                    TryDeleteFile(partial);
                }
            }
            catch (Exception ex)
            {
                AppGlobals.LogFile.LogTracing($"Clean-up of {root} stopped: {ex.Message}", LogLvl.LOG_DEBUG, null);
            }
        }

        /// <summary>
        /// Deletes a folder and everything in it, read-only files included. False, and whatever could not be
        /// deleted left in place, when something is locked.
        /// </summary>
        public static bool TryDeleteDirectory(string path)
        {
            try
            {
                if (!Directory.Exists(path))
                {
                    return true;
                }

                foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                {
                    var attributes = File.GetAttributes(file);
                    if ((attributes & FileAttributes.ReadOnly) != 0)
                    {
                        File.SetAttributes(file, attributes & ~FileAttributes.ReadOnly);
                    }
                }

                Directory.Delete(path, true);
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                AppGlobals.LogFile.LogTracing($"Could not remove {path} now; it will be tried again later: {ex.Message}", LogLvl.LOG_DEBUG, null);
                return false;
            }
        }

        #endregion

        #region Helpers

        private static HttpClient CreateHttpClient()
        {
            // The default handler follows redirects, which GitHub's download links always are.
            var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd(UpdateUtil.USER_AGENT + "/" + UpdateUtil.RunningVersionText);
            // Up to the response headers; the body is watched by the stall timeout in DownloadAsync.
            client.Timeout = TimeSpan.FromSeconds(60);
            return client;
        }

        private static void RequirePackage(UpdateCheckResult update)
        {
            if (update == null)
            {
                throw new ArgumentNullException(nameof(update));
            }

            if (!update.HasPackage || update.Version == null)
            {
                throw new UpdateException(UpdateFailure.NoPackage, $"Release {update.VersionKey} has no package to install.");
            }
        }

        private static async Task<string> ReadLimitedTextAsync(Stream stream, int limit, CancellationToken cancellationToken)
        {
            using (var buffer = new MemoryStream())
            {
                var chunk = new byte[4096];
                int read;
                while ((read = await stream.ReadAsync(chunk, 0, chunk.Length, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    buffer.Write(chunk, 0, read);
                    if (buffer.Length > limit)
                    {
                        throw new UpdateException(UpdateFailure.ChecksumMissing, "The checksum file is too large to be one.");
                    }
                }

                return Encoding.UTF8.GetString(buffer.ToArray());
            }
        }

        /// <summary>
        /// A name from the network made safe as a single file or folder name.
        /// </summary>
        private static string SafeName(string name, string fallback)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return fallback;
            }

            var invalid = Path.GetInvalidFileNameChars();
            var safe = new string(name.Trim().Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim('.', ' ');
            return safe.Length == 0 ? fallback : safe;
        }

        private static void DeleteFileOrThrow(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception ex)
            {
                throw new UpdateException(UpdateFailure.FileSystem, $"Could not delete {path}: {ex.Message}", ex);
            }
        }

        private static void TryDeleteFile(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            try
            {
                File.Delete(path);
            }
            catch (Exception ex)
            {
                AppGlobals.LogFile.LogTracing($"Could not remove {path}: {ex.Message}", LogLvl.LOG_DEBUG, null);
            }
        }

        private static void CloseQuietly(IDisposable disposable)
        {
            try
            {
                disposable.Dispose();
            }
            catch (Exception)
            {
                // Only ever used to break a pending read; the read reports the failure.
            }
        }

        #endregion
    }
}
