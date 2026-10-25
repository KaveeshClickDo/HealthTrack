using System.Net.Mail;
using HealthTrack.Services;

namespace HealthTrack.Tests.TestSupport;

/// <summary>
/// An <see cref="IEmailService"/> that keeps the messages instead of sending them, or fails like an unreachable server.
/// </summary>
public sealed class FakeEmailService : IEmailService
{
    /// <summary>The messages "sent", in order.</summary>
    public List<(string To, string Subject, string Body)> Sent { get; } = [];

    /// <summary>When true, every send throws an <see cref="SmtpException"/>.</summary>
    public bool Fail { get; set; }

    /// <inheritdoc />
    public Task SendAsync(string to, string subject, string body)
    {
        if (Fail)
        {
            throw new SmtpException("The mail server is unavailable.");
        }

        Sent.Add((to, subject, body));
        return Task.CompletedTask;
    }
}
