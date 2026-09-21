using System;

namespace NutDesk.Common.Updater
{
    /// <summary>
    /// The steps between "Install update" and a version ready to be installed.
    /// </summary>
    public enum UpdateStage
    {
        /// <summary>Fetching the published checksum, then the ZIP (or finding it already downloaded).</summary>
        Downloading,

        /// <summary>Comparing the ZIP's SHA-256 with the published one.</summary>
        Verifying,

        /// <summary>Unpacking the ZIP into the staging folder and checking the NutDesk.exe inside.</summary>
        Extracting,

        /// <summary>The new version is staged and can be installed.</summary>
        Ready,
    }

    /// <summary>
    /// A progress report of <see cref="UpdateUtil.PrepareUpdateAsync"/>. Immutable.
    /// </summary>
    public sealed class UpdateProgress
    {
        public UpdateProgress(UpdateStage stage, long bytesReceived = 0, long totalBytes = 0)
        {
            Stage = stage;
            BytesReceived = bytesReceived;
            TotalBytes = totalBytes;
        }

        public UpdateStage Stage { get; }

        /// <summary>Bytes of the ZIP received so far (Downloading only).</summary>
        public long BytesReceived { get; }

        /// <summary>Size of the ZIP in bytes, 0 when unknown (Downloading only).</summary>
        public long TotalBytes { get; }

        /// <summary>
        /// How far the download is, from 0 to 1, or null when the stage has no measurable progress (an
        /// indeterminate bar) or the size is unknown.
        /// </summary>
        public double? Fraction =>
            Stage == UpdateStage.Downloading && TotalBytes > 0
                ? Math.Max(0.0, Math.Min(1.0, (double)BytesReceived / TotalBytes))
                : (double?)null;
    }

    /// <summary>
    /// A new version downloaded, verified and unpacked, ready for <see cref="UpdateUtil.StartInstall"/> - or, when
    /// the install folder is not writable, for the user to copy by hand from <see cref="PayloadDirectory"/>.
    /// </summary>
    public sealed class PreparedUpdate
    {
        internal PreparedUpdate(UpdateCheckResult update, string archivePath, string stagingDirectory,
                                string payloadDirectory, string executablePath)
        {
            Update = update;
            ArchivePath = archivePath;
            StagingDirectory = stagingDirectory;
            PayloadDirectory = payloadDirectory;
            ExecutablePath = executablePath;
        }

        /// <summary>The check result this was prepared from.</summary>
        public UpdateCheckResult Update { get; }

        /// <summary>The version being installed (four parts), equal to the staged NutDesk.exe's assembly version.</summary>
        public Version Version => Update.Version;

        /// <summary>The verified ZIP, in UpdatesDirectory\&lt;tag&gt;\.</summary>
        public string ArchivePath { get; }

        /// <summary>UpdatesDirectory\&lt;tag&gt;\staging, where the ZIP was unpacked.</summary>
        public string StagingDirectory { get; }

        /// <summary>
        /// The folder that holds the new NutDesk.exe: the ZIP's NutDesk-X.Y.Z folder inside
        /// <see cref="StagingDirectory"/>, or the staging folder itself for a flat ZIP. Its contents are what goes
        /// over the install folder - the folder to open in Explorer for a manual update.
        /// </summary>
        public string PayloadDirectory { get; }

        /// <summary>The staged NutDesk.exe.</summary>
        public string ExecutablePath { get; }
    }
}
