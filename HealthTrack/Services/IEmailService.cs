namespace HealthTrack.Services
{
    /// <summary>
    /// Sends plain-text email: through SMTP, or into a pickup folder in development (spec 1.8).
    /// </summary>
    public interface IEmailService
    {
        /// <summary>
        /// Sends one plain-text message.
        /// </summary>
        /// <param name="to">The recipient's address.</param>
        /// <param name="subject">The subject line.</param>
        /// <param name="body">The plain-text body.</param>
        /// <returns>A task that completes when the message is handed to the server or written to the folder.</returns>
        Task SendAsync(string to, string subject, string body);
    }
}
