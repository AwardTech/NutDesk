using System;
using Octokit;

namespace NutDesk.Common.Updater
{
    /// <summary>
    /// The three answers an update check can give.
    /// </summary>
    public enum UpdateCheckOutcome
    {
        /// <summary>A release newer than the running version exists.</summary>
        UpdateAvailable,

        /// <summary>GitHub was reached and nothing newer was found.</summary>
        UpToDate,

        /// <summary>GitHub could not be asked, or did not answer usefully; see <see cref="UpdateCheckResult.Failure"/>.</summary>
        Failed,
    }

    /// <summary>
    /// What one update check found. Immutable.
    /// </summary>
    public sealed class UpdateCheckResult
    {
        internal UpdateCheckResult(UpdateCheckOutcome outcome, bool isManual, Release release, Version version,
                                   ReleaseAsset package, ReleaseAsset checksum, UpdateFailure failure,
                                   Exception error, DateTime completedUtc)
        {
            Outcome = outcome;
            IsManual = isManual;
            Release = release;
            Version = version;
            Package = package;
            ChecksumAsset = checksum;
            Failure = failure;
            Error = error;
            CompletedUtc = completedUtc;
        }

        /// <summary>Update available, up to date, or failed.</summary>
        public UpdateCheckOutcome Outcome { get; }

        /// <summary>
        /// True when the user asked for this check (menu), false for an automatic one. Manual checks always report
        /// back; automatic ones stay quiet about failures and about a skipped version.
        /// </summary>
        public bool IsManual { get; }

        /// <summary>
        /// True when GitHub answered - update available or up to date. Only then may the time of the last check
        /// (UP_LastCheck) move forward; a failed check has to be tried again.
        /// </summary>
        public bool ReachedServer => Outcome != UpdateCheckOutcome.Failed;

        /// <summary>
        /// The newest release this user accepts (drafts never, pre-releases only on the development branch). Null
        /// when the check failed or the repository has no such release. For <see cref="UpdateCheckOutcome.UpToDate"/>
        /// it is the release the running version already matches (or exceeds).
        /// </summary>
        public Release Release { get; }

        /// <summary>The four-part version read from <see cref="Release"/>'s tag, or null without a release.</summary>
        public Version Version { get; }

        /// <summary>
        /// The version as the user reads it, "1.1.0": the toast, the email and the update window show this rather
        /// than the release title, which already starts with "NutDesk". Empty without a release.
        /// </summary>
        public string VersionText => Version == null ? string.Empty : UpdateUtil.FormatVersion(Version);

        /// <summary>
        /// The value to store in UP_SkippedVersion and UP_NotifiedVersion, and to compare them with (see
        /// <see cref="IsSameRelease(string)"/>): the release tag, "v1.1.0". The tag rather than the bare version so
        /// that two pre-releases of the same version ("v1.1.0-beta.1", "v1.1.0-beta.2") stay distinct.
        /// </summary>
        public string VersionKey => Release?.TagName?.Trim() ?? string.Empty;

        /// <summary>The release's web page, where the user can always get the update by hand. Null without a release.</summary>
        public string ReleasePageUrl => Release?.HtmlUrl;

        /// <summary>The release notes as published (Markdown, usually with LF line ends). Null without a release.</summary>
        public string ReleaseNotes => Release?.Body;

        /// <summary>When the release was published, or null if unknown.</summary>
        public DateTimeOffset? PublishedAt => Release == null ? (DateTimeOffset?)null : Release.PublishedAt ?? Release.CreatedAt;

        /// <summary>
        /// The portable ZIP to install, or null when the release has none: a release made only of notes is
        /// legitimate, and the user can still open its page.
        /// </summary>
        public ReleaseAsset Package { get; }

        /// <summary>The "&lt;zip name&gt;.sha256" asset published with <see cref="Package"/>, or null.</summary>
        public ReleaseAsset ChecksumAsset { get; }

        /// <summary>
        /// True when there is something to install in place. A missing checksum does not change this: installing
        /// is then refused with <see cref="UpdateFailure.ChecksumMissing"/>, which the user should read as such.
        /// </summary>
        public bool HasPackage => Package != null;

        /// <summary>Why the check failed; <see cref="UpdateFailure.None"/> unless <see cref="Outcome"/> is Failed.</summary>
        public UpdateFailure Failure { get; }

        /// <summary>The exception behind a failed check, or null.</summary>
        public Exception Error { get; }

        /// <summary>The technical reason of a failed check (innermost exception message), for the log. Empty otherwise.</summary>
        public string ErrorDetail => UpdateException.InnermostMessage(Error);

        /// <summary>When the check finished, in UTC.</summary>
        public DateTime CompletedUtc { get; }

        /// <summary>
        /// True when <paramref name="storedKey"/> - a value of UP_SkippedVersion or UP_NotifiedVersion - names this
        /// result's release. Empty keys never match.
        /// </summary>
        public bool IsSameRelease(string storedKey)
        {
            var key = VersionKey;
            return key.Length > 0 && !string.IsNullOrWhiteSpace(storedKey) &&
                   string.Equals(key, storedKey.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>The same answer, attributed to a manual or an automatic check.</summary>
        internal UpdateCheckResult WithTrigger(bool isManual)
        {
            return isManual == IsManual
                ? this
                : new UpdateCheckResult(Outcome, isManual, Release, Version, Package, ChecksumAsset, Failure, Error, CompletedUtc);
        }

        public override string ToString()
        {
            switch (Outcome)
            {
                case UpdateCheckOutcome.UpdateAvailable:
                    return "Update available: " + VersionKey + ", package: " + (Package?.Name ?? "none") +
                           ", checksum: " + (ChecksumAsset?.Name ?? "none");
                case UpdateCheckOutcome.UpToDate:
                    return "Up to date" + (Release == null ? " (no release found)" : " (latest release " + VersionKey + ")");
                default:
                    return "Check failed (" + Failure + "): " + ErrorDetail;
            }
        }
    }
}
