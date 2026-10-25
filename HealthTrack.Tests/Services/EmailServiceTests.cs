using HealthTrack.Services;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Options;

namespace HealthTrack.Tests.Services;

/// <summary>
/// Email copies (spec 1.8): in development they are written as .eml files to a pickup folder.
/// </summary>
[TestClass]
public sealed class EmailServiceTests
{
    private static EmailService Create(SmtpOptions options) =>
        new(Options.Create(options), new HostingEnvironment { ContentRootPath = Path.GetTempPath() });

    /// <summary>With the pickup folder on, the message lands as an .eml file, and a missing folder is created.</summary>
    [TestMethod]
    public async Task SendAsync_PickupDirectory_WritesEmlFile()
    {
        var folder = Path.Combine(Path.GetTempPath(), "healthtrack-mail-" + Guid.NewGuid().ToString("N"));
        try
        {
            var email = Create(new SmtpOptions { UsePickupDirectory = true, PickupDirectory = folder });

            await email.SendAsync("client@example.test", "HealthTrack: Appointment coming up", "Your appointment is tomorrow.");

            var content = await File.ReadAllTextAsync(Directory.GetFiles(folder, "*.eml").Single());
            Assert.Contains("To: client@example.test", content);
            Assert.Contains("From: noreply@healthtrack.test", content);
            Assert.Contains("Subject: HealthTrack: Appointment coming up", content);
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }

    /// <summary>Real sending needs a server: without Smtp:Host it fails clearly (NotifyAsync logs it and carries on).</summary>
    [TestMethod]
    public async Task SendAsync_NoHostAndNoPickupDirectory_ThrowsInvalidOperation()
    {
        var email = Create(new SmtpOptions { UsePickupDirectory = false, Host = "" });

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => email.SendAsync("client@example.test", "Subject", "Body"));
    }
}
