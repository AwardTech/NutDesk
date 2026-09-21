using System;
using System.Collections.Generic;
using System.Linq;
using NutDesk.Common;

namespace NutDesk.Notifications
{
    /// <summary>
    /// How the SMTP connection is secured. Persisted as the NT_SmtpSecurity integer: never renumber.
    /// </summary>
    public enum EmailSecurity
    {
        /// <summary>SSL/TLS on port 465, STARTTLS when the server offers it on any other port.</summary>
        Auto = 0,

        /// <summary>Plain text. Only sensible for a relay on the local network.</summary>
        None = 1,

        /// <summary>Connect in plain text and upgrade with STARTTLS; fail if the server does not offer it.</summary>
        StartTls = 2,

        /// <summary>TLS from the first byte (implicit TLS, usually port 465).</summary>
        SslOnConnect = 3,
    }

    /// <summary>
    /// Everything needed to send one email: the SMTP account and the addresses.
    /// </summary>
    /// <remarks>
    /// A plain value object rather than a view over Properties.Settings, so the preferences window can send a
    /// test with what is typed in the form before anything is saved.
    /// </remarks>
    public sealed class EmailAccount
    {
        public string Host { get; set; } = string.Empty;

        public int Port { get; set; } = 587;

        public EmailSecurity Security { get; set; } = EmailSecurity.Auto;

        public bool RequiresAuth { get; set; } = true;

        public string User { get; set; } = string.Empty;

        /// <summary>The password in clear, in memory only. It is stored encrypted (DPAPI) in the settings.</summary>
        public string Password { get; set; } = string.Empty;

        public string From { get; set; } = string.Empty;

        /// <summary>The recipients as typed: one or more addresses separated by commas or semicolons.</summary>
        public string To { get; set; } = string.Empty;

        public string SubjectPrefix { get; set; } = "[NutDesk]";

        /// <summary>The individual recipient addresses, trimmed, without empty entries.</summary>
        public IReadOnlyList<string> Recipients =>
            (To ?? string.Empty)
                .Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(a => a.Trim())
                .Where(a => a.Length > 0)
                .ToList();

        /// <summary>
        /// True when there is enough to attempt a send. It says nothing about whether the server will accept it.
        /// </summary>
        public bool IsComplete =>
            !string.IsNullOrWhiteSpace(Host)
            && Port >= 1 && Port <= 65535
            && !string.IsNullOrWhiteSpace(From)
            && Recipients.Count > 0
            && (!RequiresAuth || !string.IsNullOrWhiteSpace(User));

        /// <summary>The account as currently saved in the settings.</summary>
        public static EmailAccount FromSettings()
        {
            var s = Properties.Settings.Default;

            // A setting of this type has no default value, so it reads back as null until it has been saved once.
            SerializedProtectedString password = s.NT_SmtpPassword;

            return new EmailAccount
            {
                Host = s.NT_SmtpHost ?? string.Empty,
                Port = s.NT_SmtpPort,
                Security = Enum.IsDefined(typeof(EmailSecurity), s.NT_SmtpSecurity)
                    ? (EmailSecurity)s.NT_SmtpSecurity
                    : EmailSecurity.Auto,
                RequiresAuth = s.NT_SmtpAuth,
                User = s.NT_SmtpUser ?? string.Empty,
                Password = password == null ? string.Empty : (string)password,
                From = s.NT_EmailFrom ?? string.Empty,
                To = s.NT_EmailTo ?? string.Empty,
                SubjectPrefix = s.NT_EmailSubjectPrefix ?? string.Empty,
            };
        }
    }
}
