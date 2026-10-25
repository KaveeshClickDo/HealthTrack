namespace HealthTrack.Services
{
    /// <summary>
    /// The "Smtp" section of appsettings.json: where email copies of notifications go (spec 1.8).
    /// </summary>
    public class SmtpOptions
    {
        /// <summary>The configuration section name.</summary>
        public const string SectionName = "Smtp";

        /// <summary>The SMTP server; not needed when <see cref="UsePickupDirectory"/> is on.</summary>
        public string Host { get; set; } = string.Empty;

        /// <summary>The SMTP port, usually 587.</summary>
        public int Port { get; set; } = 587;

        /// <summary>Whether the connection uses TLS.</summary>
        public bool EnableSsl { get; set; } = true;

        /// <summary>The sender address.</summary>
        public string From { get; set; } = "noreply@healthtrack.test";

        /// <summary>The SMTP user name; blank for a server without sign-in.</summary>
        public string UserName { get; set; } = string.Empty;

        /// <summary>The SMTP password; keep it in user secrets, never in the repository.</summary>
        public string Password { get; set; } = string.Empty;

        /// <summary>Whether to write .eml files to <see cref="PickupDirectory"/> instead of sending, so the demo works offline.</summary>
        public bool UsePickupDirectory { get; set; } = true;

        /// <summary>The folder for .eml files, relative to the content root unless absolute.</summary>
        public string PickupDirectory { get; set; } = "App_Data/mail";
    }
}
