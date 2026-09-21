using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MimeKit.Text;

namespace NutDesk.Notifications
{
    /// <summary>One message ready to go: everything but the account it is sent with.</summary>
    internal sealed class OutgoingEmail
    {
        internal OutgoingEmail(string subject, string body, DateTimeOffset date)
        {
            Subject = subject ?? string.Empty;
            Body = body ?? string.Empty;
            Date = date;
        }

        internal string Subject { get; }

        internal string Body { get; }

        /// <summary>The Date header: when the event happened, not when the network finally let the message out.</summary>
        internal DateTimeOffset Date { get; }
    }

    /// <summary>
    /// The one routine that talks SMTP, shared by the outbox and by the test button so that a successful test
    /// really proves the notifications will get through.
    /// </summary>
    internal static class EmailSender
    {
        /// <summary>Per network operation. Long enough for a slow relay, short enough not to hold a shutdown.</summary>
        internal const int CLIENT_TIMEOUT_MS = 30000;

        private const int MAX_ERROR_LENGTH = 300;

        /// <summary>
        /// Sends <paramref name="messages"/> over a single connection, reporting each outcome through
        /// <paramref name="onResult"/> (index, null on success or the failure). Throws only
        /// OperationCanceledException, and only when <paramref name="cancel"/> was triggered.
        /// </summary>
        /// <remarks>
        /// One connection for the whole batch: after a power cut several messages are usually waiting, and a
        /// handshake per message would multiply the time a shutdown has to wait for them.
        /// </remarks>
        internal static void SendBatch(EmailAccount account, IList<OutgoingEmail> messages, CancellationToken cancel,
                                       Action<int, Exception> onResult)
        {
            if (messages == null || messages.Count == 0)
            {
                return;
            }

            using (var client = new SmtpClient())
            {
                client.Timeout = CLIENT_TIMEOUT_MS;

                try
                {
                    // Default certificate validation is kept on purpose: the account password travels over this
                    // connection, and a mail server with a broken certificate is something to fix, not to ignore.
                    client.Connect(account.Host.Trim(), account.Port, MapSecurity(account), cancel);

                    if (account.RequiresAuth)
                    {
                        client.Authenticate(account.User.Trim(), account.Password ?? string.Empty, cancel);
                    }
                }
                catch (OperationCanceledException) when (cancel.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // Nothing can go out through a connection that was never established: every message shares
                    // the same fate, and the same explanation.
                    for (var i = 0; i < messages.Count; i++)
                    {
                        onResult(i, ex);
                    }

                    return;
                }

                for (var i = 0; i < messages.Count; i++)
                {
                    try
                    {
                        client.Send(BuildMessage(account, messages[i]), cancel);
                        onResult(i, null);
                    }
                    catch (OperationCanceledException) when (cancel.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        onResult(i, ex);

                        // A rejected message leaves the session usable for the next one; a dropped connection
                        // does not, and the rest would only fail one timeout at a time.
                        if (!client.IsConnected)
                        {
                            for (var j = i + 1; j < messages.Count; j++)
                            {
                                onResult(j, ex);
                            }

                            return;
                        }
                    }
                }

                try
                {
                    client.Disconnect(true, cancel);
                }
                catch (Exception)
                {
                    // Everything has already been handed over; a failed QUIT changes nothing.
                }
            }
        }

        /// <summary>
        /// Automatic follows the convention every provider documents: implicit TLS on 465, STARTTLS whenever the
        /// server offers it anywhere else.
        /// </summary>
        internal static SecureSocketOptions MapSecurity(EmailAccount account)
        {
            switch (account.Security)
            {
                case EmailSecurity.None:
                    return SecureSocketOptions.None;

                case EmailSecurity.StartTls:
                    return SecureSocketOptions.StartTls;

                case EmailSecurity.SslOnConnect:
                    return SecureSocketOptions.SslOnConnect;

                default:
                    return account.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable;
            }
        }

        /// <summary>
        /// Plain text only: it reads the same in every client, survives every filter, and a phone notification
        /// preview shows the first sentence instead of markup.
        /// </summary>
        private static MimeMessage BuildMessage(EmailAccount account, OutgoingEmail email)
        {
            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(account.From.Trim()));

            foreach (var recipient in account.Recipients)
            {
                message.To.Add(MailboxAddress.Parse(recipient));
            }

            message.Subject = email.Subject;
            message.Date = email.Date;
            message.Body = new TextPart(TextFormat.Plain) { Text = email.Body };

            return message;
        }

        /// <summary>
        /// A short explanation fit to show the user: the innermost message, which is where the specific cause
        /// lives ("authentication failed", "the server does not support STARTTLS", "no such host") rather than the
        /// generic wrapper around it.
        /// </summary>
        internal static string Describe(Exception ex)
        {
            if (ex == null)
            {
                return string.Empty;
            }

            if (ex is AggregateException aggregate)
            {
                ex = aggregate.Flatten().InnerException ?? ex;
            }

            if (ex is OperationCanceledException || ex is TimeoutException)
            {
                // The framework's own wording, which Windows translates.
                return new TimeoutException().Message;
            }

            string message = null;

            for (var current = ex; current != null; current = current.InnerException)
            {
                if (!string.IsNullOrWhiteSpace(current.Message))
                {
                    message = current.Message;
                }
            }

            if (message == null)
            {
                return ex.GetType().Name;
            }

            // Some exceptions carry a paragraph of advice after the first line; the first line is the cause.
            message = message.Trim();
            var lineEnd = message.IndexOfAny(new[] { '\r', '\n' });

            if (lineEnd > 0)
            {
                message = message.Substring(0, lineEnd).Trim();
            }

            return message.Length > MAX_ERROR_LENGTH ? message.Substring(0, MAX_ERROR_LENGTH) + "..." : message;
        }

        /// <summary>
        /// Names the first thing that keeps <paramref name="account"/> from being complete, using the labels of the
        /// preferences form so the user knows which field to look at.
        /// </summary>
        internal static string DescribeIncomplete(EmailAccount account, CultureInfo culture)
        {
            if (account == null || string.IsNullOrWhiteSpace(account.Host))
            {
                return NotificationText.Get("Pref_Email_Server", culture);
            }

            if (account.Port < 1 || account.Port > 65535)
            {
                return NotificationText.Get("Pref_Email_Port", culture);
            }

            if (account.RequiresAuth && string.IsNullOrWhiteSpace(account.User))
            {
                return NotificationText.Get("Pref_Email_User", culture);
            }

            var field = string.IsNullOrWhiteSpace(account.From)
                ? NotificationText.Get("Pref_Email_From", culture)
                : NotificationText.Get("Pref_Email_To", culture);

            return new StringBuilder(field).Append(": ")
                .Append(NotificationText.Get("Pref_Email_InvalidAddress", culture)).ToString();
        }
    }
}
