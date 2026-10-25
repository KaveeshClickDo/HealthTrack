using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace HealthTrack.Services
{
    /// <summary>
    /// Sends email with System.Net.Mail. With UsePickupDirectory on, each message is written as an .eml file to the
    /// pickup folder (App_Data/mail by default), so the demo works without a mail server.
    /// </summary>
    public class EmailService : IEmailService
    {
        // A slow mail server must not hold up the request that raised the notification for long.
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        private readonly SmtpOptions _options;
        private readonly IHostEnvironment _environment;

        /// <summary>
        /// Creates the service.
        /// </summary>
        /// <param name="options">The "Smtp" settings.</param>
        /// <param name="environment">Supplies the content root for a relative pickup folder.</param>
        public EmailService(IOptions<SmtpOptions> options, IHostEnvironment environment)
        {
            _options = options.Value;
            _environment = environment;
        }

        /// <inheritdoc />
        public async Task SendAsync(string to, string subject, string body)
        {
            using var message = new MailMessage(_options.From, to, subject, body);
            using var client = CreateClient();
            // SmtpClient.Timeout covers only the synchronous Send, so the async send gets its own deadline.
            using var deadline = new CancellationTokenSource(Timeout);
            await client.SendMailAsync(message, deadline.Token);
        }

        private SmtpClient CreateClient()
        {
            if (_options.UsePickupDirectory)
            {
                // Path.Combine keeps an absolute PickupDirectory as it is.
                var folder = Path.Combine(_environment.ContentRootPath, _options.PickupDirectory);
                Directory.CreateDirectory(folder);
                return new SmtpClient
                {
                    DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory,
                    PickupDirectoryLocation = folder
                };
            }

            if (string.IsNullOrWhiteSpace(_options.Host))
            {
                throw new InvalidOperationException("Smtp:Host is not set. Set it, or turn Smtp:UsePickupDirectory on.");
            }

            var client = new SmtpClient(_options.Host, _options.Port) { EnableSsl = _options.EnableSsl };
            if (!string.IsNullOrEmpty(_options.UserName))
            {
                client.Credentials = new NetworkCredential(_options.UserName, _options.Password);
            }

            return client;
        }
    }
}
