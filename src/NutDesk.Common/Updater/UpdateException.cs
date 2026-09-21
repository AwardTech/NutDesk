using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using Octokit;

namespace NutDesk.Common.Updater
{
    /// <summary>
    /// Why an update check, download or installation failed. The user interface turns it into a sentence in the
    /// user's language; the technical detail stays in the log.
    /// </summary>
    public enum UpdateFailure
    {
        /// <summary>Nothing failed.</summary>
        None = 0,

        /// <summary>GitHub or the download server could not be reached, or the transfer broke off or timed out.</summary>
        Network,

        /// <summary>GitHub refused the request because this address made too many of them.</summary>
        RateLimited,

        /// <summary>GitHub answered, but with an error.</summary>
        ServerError,

        /// <summary>The release carries no portable ZIP to install.</summary>
        NoPackage,

        /// <summary>The release publishes no "&lt;zip name&gt;.sha256" file, or one without a usable hash.</summary>
        ChecksumMissing,

        /// <summary>The downloaded ZIP does not match the published SHA-256. The file has been deleted.</summary>
        ChecksumMismatch,

        /// <summary>The ZIP is damaged, holds an entry that would land outside its folder, or holds no NutDesk.exe.</summary>
        InvalidPackage,

        /// <summary>The NutDesk.exe in the package is not the version the release announces.</summary>
        VersionMismatch,

        /// <summary>A file or folder under the updates folder could not be written or removed.</summary>
        FileSystem,

        /// <summary>The new version could not be started to install itself.</summary>
        LaunchFailed,

        /// <summary>Anything else; see the exception for the detail.</summary>
        Unexpected,
    }

    /// <summary>
    /// A failure of the updater, with its <see cref="UpdateFailure"/> kind. The message is technical English meant
    /// for the log; the user interface words the failure from <see cref="Failure"/>.
    /// </summary>
    public sealed class UpdateException : Exception
    {
        public UpdateException(UpdateFailure failure, string message, Exception innerException = null)
            : base(message, innerException)
        {
            Failure = failure;
        }

        /// <summary>What went wrong.</summary>
        public UpdateFailure Failure { get; }

        /// <summary>
        /// Sorts an exception thrown by Octokit, HttpClient or the file system into an <see cref="UpdateFailure"/>.
        /// An <see cref="UpdateException"/> keeps its own kind.
        /// </summary>
        public static UpdateFailure Classify(Exception ex)
        {
            if (ex is AggregateException aggregate && aggregate.InnerExceptions.Count == 1)
            {
                ex = aggregate.InnerException;
            }

            switch (ex)
            {
                case null:
                    return UpdateFailure.None;
                case UpdateException update:
                    return update.Failure;
                case RateLimitExceededException _:
                    return UpdateFailure.RateLimited;
                case ApiException api:
                    // An unauthenticated client that has used up its allowance gets a plain 403 on some paths
                    // rather than the dedicated exception.
                    return api.StatusCode == HttpStatusCode.Forbidden &&
                           (api.Message ?? string.Empty).IndexOf("rate limit", StringComparison.OrdinalIgnoreCase) >= 0
                        ? UpdateFailure.RateLimited
                        : UpdateFailure.ServerError;
                case HttpRequestException _:
                case WebException _:
                case SocketException _:
                    return UpdateFailure.Network;
                case OperationCanceledException _:
                    // Reaching here means nobody asked for a cancellation (those are handled as such by the callers):
                    // HttpClient reports its timeout this way.
                    return UpdateFailure.Network;
                case IOException _:
                case UnauthorizedAccessException _:
                    return UpdateFailure.FileSystem;
                default:
                    return UpdateFailure.Unexpected;
            }
        }

        /// <summary>
        /// The innermost message of an exception chain: "An error occurred while sending the request" says less
        /// than the "The remote name could not be resolved" underneath it.
        /// </summary>
        public static string InnermostMessage(Exception ex)
        {
            if (ex == null)
            {
                return string.Empty;
            }

            while (ex.InnerException != null)
            {
                ex = ex.InnerException;
            }

            return ex.Message;
        }
    }
}
