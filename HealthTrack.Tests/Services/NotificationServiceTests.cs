using HealthTrack.Models.Enums;
using HealthTrack.Security;
using HealthTrack.Services;
using HealthTrack.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Tests.Services;

/// <summary>
/// Creating notifications: saved unread with the clock's time, links limited to local paths.
/// </summary>
[TestClass]
public sealed class NotificationServiceTests
{
    /// <summary>A notification is saved unread with the clock's time and its local link.</summary>
    [TestMethod]
    public async Task NotifyAsync_LocalLink_SavesUnreadNotification()
    {
        await using var host = await TestHost.CreateAsync();
        var user = await host.CreateUserAsync("client@example.test", AppRoles.Client);

        await host.Get<INotificationService>().NotifyAsync(user.Id, NotificationType.PlanAssigned,
            "New workout plan", "Your trainer activated a plan.", "/Client/Workouts/MyPlan");

        var saved = await host.Db.Notifications.SingleAsync();
        Assert.AreEqual(user.Id, saved.UserId);
        Assert.AreEqual(NotificationType.PlanAssigned, saved.Type);
        Assert.AreEqual("/Client/Workouts/MyPlan", saved.LinkUrl);
        Assert.IsFalse(saved.IsRead);
        Assert.AreEqual(host.Clock.Now, saved.CreatedAt);
    }

    /// <summary>Links that leave the site (absolute, protocol-relative or script URLs) are refused and nothing is saved.</summary>
    [TestMethod]
    [DataRow("https://evil.example/login")]
    [DataRow("//evil.example")]
    [DataRow("/\\evil.example")]
    [DataRow("javascript:alert(1)")]
    [DataRow("Client/Appointments")]
    public async Task NotifyAsync_NonLocalLink_Throws(string linkUrl)
    {
        await using var host = await TestHost.CreateAsync();
        var user = await host.CreateUserAsync("client@example.test", AppRoles.Client);

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => host.Get<INotificationService>()
            .NotifyAsync(user.Id, NotificationType.System, "Title", "Message", linkUrl));

        Assert.IsFalse(await host.Db.Notifications.AnyAsync());
    }

    /// <summary>A notification without a link is fine.</summary>
    [TestMethod]
    public async Task NotifyAsync_NoLink_Saves()
    {
        await using var host = await TestHost.CreateAsync();
        var user = await host.CreateUserAsync("client@example.test", AppRoles.Client);

        await host.Get<INotificationService>().NotifyAsync(user.Id, NotificationType.System, "Welcome", "Hello!");

        Assert.IsNull((await host.Db.Notifications.SingleAsync()).LinkUrl);
    }
}
