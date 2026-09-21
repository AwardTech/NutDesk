using System;
using System.Globalization;
using NutDesk.Common.Updater;

namespace NutDesk.Updating
{
    /// <summary>
    /// The updater's failures and results worded for the user, in the language of the user interface. The updater
    /// itself (NutDesk.Common) only knows <see cref="UpdateFailure"/> kinds and English log text.
    /// </summary>
    internal static class UpdateText
    {
        /// <summary>
        /// Why an update step failed, as a sentence. <paramref name="detail"/> - the technical reason - is only
        /// shown for the kinds whose sentence has a place for it (file system, launch, unexpected).
        /// </summary>
        internal static string Describe(UpdateFailure failure, string detail)
        {
            detail = string.IsNullOrWhiteSpace(detail) ? "?" : detail.Trim();

            switch (failure)
            {
                case UpdateFailure.Network:
                    return Properties.Resources.Update_Failure_Network;
                case UpdateFailure.RateLimited:
                    return Properties.Resources.Update_Failure_RateLimited;
                case UpdateFailure.ServerError:
                    return Properties.Resources.Update_Failure_ServerError;
                case UpdateFailure.NoPackage:
                    return Properties.Resources.Update_Failure_NoPackage;
                case UpdateFailure.ChecksumMissing:
                    return Properties.Resources.Update_Failure_ChecksumMissing;
                case UpdateFailure.ChecksumMismatch:
                    return Properties.Resources.Update_Failure_ChecksumMismatch;
                case UpdateFailure.InvalidPackage:
                    return Properties.Resources.Update_Failure_InvalidPackage;
                case UpdateFailure.VersionMismatch:
                    return Properties.Resources.Update_Failure_VersionMismatch;
                case UpdateFailure.FileSystem:
                    return Format(Properties.Resources.Update_Failure_FileSystem, detail);
                case UpdateFailure.LaunchFailed:
                    return Format(Properties.Resources.Update_Failure_LaunchFailed, detail);
                default:
                    return Format(Properties.Resources.Update_Failure_Unexpected, detail);
            }
        }

        /// <summary>
        /// Why an update step failed, from what it threw: an <see cref="UpdateException"/> by its kind, anything else
        /// by <see cref="UpdateException.Classify(Exception)"/>.
        /// </summary>
        internal static string Describe(Exception ex)
        {
            return Describe(UpdateException.Classify(ex), UpdateException.InnermostMessage(ex));
        }

        /// <summary>Why a check failed.</summary>
        internal static string Describe(UpdateCheckResult result)
        {
            return result == null ? Describe(UpdateFailure.Unexpected, null) : Describe(result.Failure, result.ErrorDetail);
        }

        /// <summary>"Could not check for updates: &lt;reason&gt;".</summary>
        internal static string CheckFailedMessage(UpdateCheckResult result)
        {
            return Format(Properties.Resources.Update_CheckFailed, Describe(result));
        }

        /// <summary>"You are running the latest version (X)."</summary>
        internal static string UpToDateMessage()
        {
            return Format(Properties.Resources.Update_UpToDate, UpdateUtil.RunningVersionText);
        }

        /// <summary>string.Format that cannot throw: a translation with a broken placeholder still gives its text.</summary>
        internal static string Format(string template, params object[] args)
        {
            if (string.IsNullOrEmpty(template))
            {
                return string.Empty;
            }

            try
            {
                return string.Format(CultureInfo.CurrentCulture, template, args);
            }
            catch (FormatException)
            {
                return template;
            }
        }
    }
}
