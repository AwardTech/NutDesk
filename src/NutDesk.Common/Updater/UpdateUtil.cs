using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Octokit;

namespace NutDesk.Common.Updater
{
    /// <summary>
    /// Looks for NutDesk releases on GitHub, and drives the preparation and the start of an in-place update. The
    /// application uses the single instance <see cref="AppGlobals.UpdateController"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The check reads the unauthenticated releases API, which GitHub limits to 60 requests an hour per address. One
    /// check costs exactly one request, and automatic checks happen at most once a day, a week or a month (see
    /// <see cref="UpdateCheckDelayPassed(int, DateTime)"/>), so that limit is nowhere near.
    /// </para>
    /// <para>
    /// Threading: the async methods resume on the synchronization context of the thread that called them, so a
    /// caller on the UI thread gets its result, and its exceptions, back on the UI thread. Progress goes through
    /// the <see cref="IProgress{T}"/> the caller passes; a <see cref="Progress{T}"/> created on the UI thread
    /// reports there. Nothing in this class raises events.
    /// </para>
    /// </remarks>
    public class UpdateUtil
    {
        private const string REPOSITORY_OWNER = "kriseniprak";
        private const string REPOSITORY_NAME = "NutDesk";

        /// <summary>
        /// The product name sent as User-Agent. GitHub rejects API calls without one and asks for one naming the
        /// application.
        /// </summary>
        internal const string USER_AGENT = REPOSITORY_NAME;

        // The API returns releases newest first; one page of this size holds far more history than is ever needed
        // and keeps the check to a single request.
        private const int RELEASES_PAGE_SIZE = 30;

        // Well under Octokit's default of 100 seconds: an automatic check that cannot get through is retried later
        // anyway, and a manual one should not keep the user waiting that long for "could not check".
        private static readonly TimeSpan REQUEST_TIMEOUT = TimeSpan.FromSeconds(30);

#if DEBUG
        /// <summary>
        /// Debug builds only: when set, the API base address to query instead of api.github.com, so the whole
        /// update path can be exercised against a local fake server. Octokit appends "api/v3/" to any host other
        /// than github.com. Release builds do not contain this hook.
        /// </summary>
        public const string API_URL_OVERRIDE_VARIABLE = "NUTDESK_UPDATE_API_URL";
#endif

        private static readonly Lazy<Version> runningVersion = new Lazy<Version>(
            () => Normalize(typeof(UpdateUtil).Assembly.GetName().Version));

        private readonly object sync = new object();
        private Task<UpdateCheckResult> checkTask;
        private bool isPreparing;
        private volatile bool isInstallStarted;
        private volatile UpdateCheckResult lastResult;

        #region Versions

        /// <summary>
        /// The running version, four parts. The same assembly version as <see cref="AppGlobals.ProgramVersion"/>;
        /// both assemblies are stamped from Directory.Build.props.
        /// </summary>
        public static Version RunningVersion => runningVersion.Value;

        /// <summary>The running version as the user reads it, "1.0.0".</summary>
        public static string RunningVersionText => FormatVersion(RunningVersion);

        /// <summary>
        /// A version as the user reads it: three parts, "1.1.0", or four when the fourth is not zero.
        /// </summary>
        public static string FormatVersion(Version version)
        {
            if (version == null)
            {
                return string.Empty;
            }

            var normalized = Normalize(version);
            return normalized.Revision > 0 ? normalized.ToString(4) : normalized.ToString(3);
        }

        /// <summary>
        /// Turn a release tag such as "v1.2.0", "1.2" or "v1.3.0-beta.1" into a four part version that compares
        /// correctly with the assembly version.
        /// </summary>
        /// <remarks>
        /// System.Version treats a missing part as lower than zero, so "1.0.0" sorts below the running "1.0.0.0"
        /// and a three part tag can never equal a four part assembly version. Missing parts are therefore filled
        /// with zeros. A pre-release suffix is dropped: "v1.3.0-beta.1" counts as 1.3.0, newer than any 1.2 but
        /// not newer than a 1.3.0 already installed.
        /// </remarks>
        public static bool TryParseReleaseVersion(string tag, out Version version)
        {
            version = null;
            if (string.IsNullOrWhiteSpace(tag))
            {
                return false;
            }

            var match = Regex.Match(tag.Trim(), @"^[vV]?(\d+(?:\.\d+){0,3})(?:[-+].*)?$");
            if (!match.Success)
            {
                return false;
            }

            var parts = new List<int>();
            foreach (var part in match.Groups[1].Value.Split('.'))
            {
                // TryParse: a tag is outside data, and a run of digits too long for an int must not throw.
                if (!int.TryParse(part, out int number))
                {
                    return false;
                }
                parts.Add(number);
            }
            while (parts.Count < 4)
            {
                parts.Add(0);
            }

            version = new Version(parts[0], parts[1], parts[2], parts[3]);
            return true;
        }

        /// <summary>
        /// Normalise a version to four parts, for the same reason as in <see cref="TryParseReleaseVersion"/>.
        /// </summary>
        public static Version Normalize(Version v)
        {
            return new Version(v.Major, v.Minor, Math.Max(v.Build, 0), Math.Max(v.Revision, 0));
        }

        #endregion

        #region State

        /// <summary>The result of the last completed check, or null before the first one.</summary>
        public UpdateCheckResult LastResult => lastResult;

        /// <summary>True while a check is talking to GitHub.</summary>
        public bool IsCheckInProgress
        {
            get
            {
                lock (sync)
                {
                    return checkTask != null && !checkTask.IsCompleted;
                }
            }
        }

        /// <summary>True while <see cref="PrepareUpdateAsync"/> is downloading, verifying or extracting.</summary>
        public bool IsPreparing
        {
            get
            {
                lock (sync)
                {
                    return isPreparing;
                }
            }
        }

        /// <summary>
        /// True once <see cref="StartInstall"/> has launched the new version: from then on the application is
        /// expected to exit, and nothing else about updates should start.
        /// </summary>
        public bool IsInstallStarted => isInstallStarted;

        /// <summary>
        /// True while a check, a preparation or an installation is under way. An automatic check should not start
        /// then.
        /// </summary>
        public bool IsBusy => IsCheckInProgress || IsPreparing || IsInstallStarted;

        #endregion

        #region Check

        /// <summary>
        /// Asks GitHub for the newest release this user accepts and compares it with the running version.
        /// </summary>
        /// <param name="acceptPreRelease">True on the development branch (UP_Branch = 1), where pre-releases are offered too.</param>
        /// <param name="manual">True when the user asked for the check; carried into the result as <see cref="UpdateCheckResult.IsManual"/>.</param>
        /// <returns>
        /// Never faults: a failure is a result with <see cref="UpdateCheckOutcome.Failed"/>. Completes on the
        /// caller's synchronization context. A call made while another check is running shares that check's
        /// request (and its pre-release choice) instead of sending a second one.
        /// </returns>
        public async Task<UpdateCheckResult> CheckForUpdateAsync(bool acceptPreRelease, bool manual)
        {
            Task<UpdateCheckResult> shared;
            lock (sync)
            {
                if (checkTask == null || checkTask.IsCompleted)
                {
                    checkTask = RunCheckAsync(acceptPreRelease);
                }

                shared = checkTask;
            }

            var result = (await shared).WithTrigger(manual);
            lastResult = result;
            return result;
        }

        private async Task<UpdateCheckResult> RunCheckAsync(bool acceptPreRelease)
        {
            try
            {
                var client = CreateClient();
                var releases = await client.Repository.Release.GetAll(REPOSITORY_OWNER, REPOSITORY_NAME,
                    new ApiOptions { PageSize = RELEASES_PAGE_SIZE, PageCount = 1, StartPage = 1 }).ConfigureAwait(false);

                // Pick by version, not by list position: a fix published for an older line (a 1.0.3 after a
                // 1.1.0) would otherwise be offered to someone already on 1.1.0. On equal versions the final
                // release is preferred over its pre-releases.
                Release best = null;
                Version bestVersion = null;
                foreach (Release rel in releases)
                {
                    if (rel.Draft || (rel.Prerelease && !acceptPreRelease))
                    {
                        continue;
                    }

                    if (!TryParseReleaseVersion(rel.TagName, out Version relVersion))
                    {
                        AppGlobals.LogFile.LogTracing($"Ignoring release with unreadable tag '{rel.TagName}'.",
                                                      LogLvl.LOG_DEBUG, this);
                        continue;
                    }

                    if (best == null || relVersion > bestVersion ||
                        (relVersion == bestVersion && best.Prerelease && !rel.Prerelease))
                    {
                        best = rel;
                        bestVersion = relVersion;
                    }
                }

                UpdateCheckResult result;
                if (best == null)
                {
                    result = new UpdateCheckResult(UpdateCheckOutcome.UpToDate, false, null, null, null, null,
                                                   UpdateFailure.None, null, DateTime.UtcNow);
                }
                else
                {
                    var package = SelectPackage(best);
                    var outcome = bestVersion > RunningVersion ? UpdateCheckOutcome.UpdateAvailable : UpdateCheckOutcome.UpToDate;
                    result = new UpdateCheckResult(outcome, false, best, bestVersion, package, SelectChecksum(best, package),
                                                   UpdateFailure.None, null, DateTime.UtcNow);
                }

                AppGlobals.LogFile.LogTracing($"Update check (acceptPreRelease = {acceptPreRelease}, running {RunningVersionText}): {result}.",
                                              LogLvl.LOG_DEBUG, this);
                return result;
            }
            catch (Exception ex)
            {
                var failure = UpdateException.Classify(ex);
                if (failure == UpdateFailure.None)
                {
                    failure = UpdateFailure.Unexpected;
                }

                AppGlobals.LogFile.LogTracing($"Update check failed ({failure}): {UpdateException.InnermostMessage(ex)}",
                                              LogLvl.LOG_WARNING, this);
                AppGlobals.LogFile.LogException(ex, this);
                return new UpdateCheckResult(UpdateCheckOutcome.Failed, false, null, null, null, null, failure, ex,
                                             DateTime.UtcNow);
            }
        }

        private GitHubClient CreateClient()
        {
            var header = new ProductHeaderValue(USER_AGENT, RunningVersionText);
            GitHubClient client = null;

#if DEBUG
            var overrideUrl = Environment.GetEnvironmentVariable(API_URL_OVERRIDE_VARIABLE);
            if (!string.IsNullOrWhiteSpace(overrideUrl))
            {
                if (Uri.TryCreate(overrideUrl.Trim(), UriKind.Absolute, out Uri baseAddress))
                {
                    AppGlobals.LogFile.LogTracing($"Update API redirected to {baseAddress} by {API_URL_OVERRIDE_VARIABLE}.",
                                                  LogLvl.LOG_WARNING, this);
                    client = new GitHubClient(header, baseAddress);
                }
                else
                {
                    AppGlobals.LogFile.LogTracing($"Ignoring {API_URL_OVERRIDE_VARIABLE}: '{overrideUrl}' is not an absolute URL.",
                                                  LogLvl.LOG_WARNING, this);
                }
            }
#endif

            if (client == null)
            {
                client = new GitHubClient(header);
            }

            client.SetRequestTimeout(REQUEST_TIMEOUT);
            return client;
        }

        /// <summary>
        /// Pick the portable ZIP of a release. A release could carry more than one archive (sources, symbols), so
        /// the one named "portable" wins; any other ZIP is still better than nothing.
        /// </summary>
        private static ReleaseAsset SelectPackage(Release release)
        {
            if (release?.Assets == null)
            {
                return null;
            }

            var zips = release.Assets
                              .Where(asset => asset?.Name != null &&
                                              asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                              .ToList();

            return zips.FirstOrDefault(asset => asset.Name.IndexOf("portable", StringComparison.OrdinalIgnoreCase) >= 0)
                   ?? zips.FirstOrDefault();
        }

        /// <summary>
        /// The "&lt;zip name&gt;.sha256" published next to the package, or null.
        /// </summary>
        private static ReleaseAsset SelectChecksum(Release release, ReleaseAsset package)
        {
            if (package == null || release?.Assets == null)
            {
                return null;
            }

            var name = package.Name + UpdateInstaller.ChecksumExtension;
            return release.Assets.FirstOrDefault(asset => string.Equals(asset?.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        #endregion

        #region Automatic check interval

        /// <summary>
        /// The time between two automatic checks for a UP_AutoChkDelay value: 0 = daily (1 day), 1 = weekly
        /// (7 days), 2 = monthly (30 days). Elapsed time, not calendar units. Anything else counts as weekly, the
        /// default.
        /// </summary>
        public static TimeSpan GetCheckInterval(int autoCheckDelay)
        {
            switch (autoCheckDelay)
            {
                case 0:
                    return TimeSpan.FromDays(1);
                case 2:
                    return TimeSpan.FromDays(30);
                default:
                    return TimeSpan.FromDays(7);
            }
        }

        /// <summary>
        /// Determine if the interval between update checks has passed.
        /// </summary>
        /// <param name="autoCheckDelay">The UP_AutoChkDelay setting (see <see cref="GetCheckInterval(int)"/>).</param>
        /// <param name="lastChecked">When a check last reached GitHub (UP_LastCheck); DateTime.MinValue for never.</param>
        /// <returns>True if an automatic check is due.</returns>
        public static bool UpdateCheckDelayPassed(int autoCheckDelay, DateTime lastChecked)
        {
            if (lastChecked == DateTime.MinValue)
            {
                return true;
            }

            var now = DateTime.Now;

            // A last check in the future means the clock has been set back since. Waiting for it to catch up could
            // mean months without a check, so check now and start counting again from a sane time.
            if (lastChecked > now)
            {
                return true;
            }

            return now - lastChecked >= GetCheckInterval(autoCheckDelay);
        }

        #endregion

        #region Preparation and installation

        /// <summary>
        /// Downloads the release's ZIP into UpdatesDirectory\&lt;tag&gt;\, verifies it against the published
        /// SHA-256, unpacks it into the staging folder and checks the version of the NutDesk.exe inside.
        /// </summary>
        /// <param name="update">A result with a package (<see cref="UpdateCheckResult.HasPackage"/>).</param>
        /// <param name="progress">
        /// Receives a report on entering each <see cref="UpdateStage"/> and, while downloading, a few times a second.
        /// Reports come from worker threads: pass a <see cref="Progress{T}"/> created on the UI thread to get them
        /// there. May be null.
        /// </param>
        /// <param name="cancellationToken">Cancels the download (and any later step not yet begun).</param>
        /// <returns>The staged update. Completes on the caller's synchronization context.</returns>
        /// <exception cref="UpdateException">The preparation failed; <see cref="UpdateException.Failure"/> says why.</exception>
        /// <exception cref="OperationCanceledException">The token was cancelled. Nothing half-done is left in place.</exception>
        /// <exception cref="InvalidOperationException">Another preparation is already running.</exception>
        public async Task<PreparedUpdate> PrepareUpdateAsync(UpdateCheckResult update, IProgress<UpdateProgress> progress,
                                                             CancellationToken cancellationToken)
        {
            if (update == null)
            {
                throw new ArgumentNullException(nameof(update));
            }

            lock (sync)
            {
                if (isPreparing)
                {
                    throw new InvalidOperationException("An update is already being prepared.");
                }
                isPreparing = true;
            }

            try
            {
                if (!update.HasPackage || update.Version == null)
                {
                    throw new UpdateException(UpdateFailure.NoPackage, $"Release {update.VersionKey} has no package to install.");
                }

                AppGlobals.LogFile.LogTracing($"Preparing the update to {update.VersionKey}.", LogLvl.LOG_NOTICE, this);

                progress?.Report(new UpdateProgress(UpdateStage.Downloading, 0, update.Package.Size));
                var expectedHash = await UpdateInstaller.FetchExpectedHashAsync(update, cancellationToken);
                var archive = await UpdateInstaller.DownloadAsync(update, expectedHash, progress, cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new UpdateProgress(UpdateStage.Verifying));
                await UpdateInstaller.VerifyAsync(archive, expectedHash, cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new UpdateProgress(UpdateStage.Extracting));
                var prepared = await UpdateInstaller.ExtractAsync(update, archive, cancellationToken);

                progress?.Report(new UpdateProgress(UpdateStage.Ready));
                AppGlobals.LogFile.LogTracing($"Update {update.VersionKey} staged in {prepared.PayloadDirectory}.", LogLvl.LOG_NOTICE, this);
                return prepared;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                AppGlobals.LogFile.LogTracing("Update preparation cancelled.", LogLvl.LOG_NOTICE, this);
                throw;
            }
            catch (UpdateException ex)
            {
                AppGlobals.LogFile.LogTracing($"Update preparation failed ({ex.Failure}): {ex.Message}", LogLvl.LOG_ERROR, this);
                AppGlobals.LogFile.LogException(ex, this);
                throw;
            }
            catch (Exception ex)
            {
                var failure = UpdateException.Classify(ex);
                AppGlobals.LogFile.LogTracing($"Update preparation failed ({failure}): {ex.Message}", LogLvl.LOG_ERROR, this);
                AppGlobals.LogFile.LogException(ex, this);
                throw new UpdateException(failure == UpdateFailure.None ? UpdateFailure.Unexpected : failure, ex.Message, ex);
            }
            finally
            {
                lock (sync)
                {
                    isPreparing = false;
                }
            }
        }

        /// <summary>
        /// Starts the staged NutDesk.exe in apply mode (see <see cref="UpdateCommandLine"/>) to install itself over
        /// <paramref name="installDirectory"/>. The caller must then exit the application for real - not to the tray,
        /// with the settings saved and the email outbox flushed as on a normal exit, which Application.Exit() does -
        /// because the new version waits for this process to end (60 seconds at most) before copying anything.
        /// </summary>
        /// <param name="update">What <see cref="PrepareUpdateAsync"/> returned.</param>
        /// <param name="installDirectory">
        /// The folder to update, normally Application.StartupPath. Check it first with
        /// <see cref="UpdateInstaller.CanInstallInPlace(string)"/>; when that is false, offer the manual update instead.
        /// </param>
        /// <param name="persistData">
        /// True when this copy runs with -PersistDataInStartupPath (<see cref="AppGlobals.PersistDataInStartupPathRequested"/>),
        /// so the updated copy starts the same way.
        /// </param>
        /// <exception cref="UpdateException">With <see cref="UpdateFailure.LaunchFailed"/>: nothing was started, keep running.</exception>
        public void StartInstall(PreparedUpdate update, string installDirectory, bool persistData)
        {
            UpdateInstaller.LaunchApplyMode(update, installDirectory, persistData);
            isInstallStarted = true;
        }

        #endregion
    }
}
