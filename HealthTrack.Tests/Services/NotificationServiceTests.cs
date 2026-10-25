using HealthTrack.Models.Entities;
using HealthTrack.Models.Enums;
using HealthTrack.Models.ViewModels;
using HealthTrack.Security;
using HealthTrack.Services;
using HealthTrack.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Tests.Services;

/// <summary>
/// Notifications (spec 1.8, UC06): creating them with their email copies, reading and clearing them, and the reminder
/// settings.
/// </summary>
[TestClass]
public sealed class NotificationServiceTests
{
    private static async Task<Notification> AddAsync(TestHost host, string userId, string title, int minutesAgo = 0, bool isRead = false)
    {
        var notification = new Notification
        {
            UserId = userId,
            Type = NotificationType.System,
            Title = title,
            Message = "Message",
            IsRead = isRead,
            CreatedAt = host.Clock.Now.AddMinutes(-minutesAgo)
        };
        host.Db.Notifications.Add(notification);
        await host.Db.SaveChangesAsync();
        return notification;
    }

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

    /// <summary>Spec 1.8: with email copies on, the notification is emailed too and EmailSentAt is set.</summary>
    [TestMethod]
    public async Task NotifyAsync_EmailCopiesOn_SendsEmailAndSetsEmailSentAt()
    {
        await using var host = await TestHost.CreateAsync();
        var user = await host.CreateUserAsync("client@example.test", AppRoles.Client);
        await host.SetPreferencesAsync(user.Id, p => p.EmailEnabled = true);

        var notification = await host.Get<INotificationService>().NotifyAsync(user.Id, NotificationType.NewFeedback,
            "New feedback", "Great squats today.");

        var (to, subject, body) = host.Email.Sent.Single();
        Assert.AreEqual("client@example.test", to);
        Assert.AreEqual("HealthTrack: New feedback", subject);
        Assert.Contains("Great squats today.", body);
        var saved = await host.Db.Notifications.AsNoTracking().SingleAsync(n => n.NotificationId == notification.NotificationId);
        Assert.AreEqual(host.Clock.Now, saved.EmailSentAt);
    }

    /// <summary>Email copies are off by default, and suspended accounts never get them.</summary>
    [TestMethod]
    public async Task NotifyAsync_EmailOffOrAccountSuspended_NoEmail()
    {
        await using var host = await TestHost.CreateAsync();
        var defaults = await host.CreateUserAsync("client@example.test", AppRoles.Client);
        var suspended = await host.CreateUserAsync("suspended@example.test", AppRoles.Client, AccountStatus.Suspended);
        await host.SetPreferencesAsync(defaults.Id, _ => { });
        await host.SetPreferencesAsync(suspended.Id, p => p.EmailEnabled = true);
        var notifications = host.Get<INotificationService>();

        await notifications.NotifyAsync(defaults.Id, NotificationType.System, "Hello", "Message");
        await notifications.NotifyAsync(suspended.Id, NotificationType.System, "Hello", "Message");

        Assert.IsEmpty(host.Email.Sent);
        Assert.IsTrue(await host.Db.Notifications.AllAsync(n => n.EmailSentAt == null));
    }

    /// <summary>A mail failure is logged, never thrown: the notification is still saved, without EmailSentAt.</summary>
    [TestMethod]
    public async Task NotifyAsync_EmailFails_StillSavesNotification()
    {
        await using var host = await TestHost.CreateAsync();
        var user = await host.CreateUserAsync("client@example.test", AppRoles.Client);
        await host.SetPreferencesAsync(user.Id, p => p.EmailEnabled = true);
        host.Email.Fail = true;

        await host.Get<INotificationService>().NotifyAsync(user.Id, NotificationType.System, "Hello", "Message");

        Assert.IsNull((await host.Db.Notifications.SingleAsync()).EmailSentAt);
    }

    /// <summary>The bell gets the newest unread notifications only, and the count of all unread ones.</summary>
    [TestMethod]
    public async Task GetUnreadAsync_MixedNotifications_NewestUnreadFirst()
    {
        await using var host = await TestHost.CreateAsync();
        var user = await host.CreateUserAsync("client@example.test", AppRoles.Client);
        var other = await host.CreateUserAsync("other@example.test", AppRoles.Client);
        await AddAsync(host, user.Id, "Oldest", minutesAgo: 30);
        await AddAsync(host, user.Id, "Read", minutesAgo: 20, isRead: true);
        await AddAsync(host, user.Id, "Middle", minutesAgo: 10);
        await AddAsync(host, user.Id, "Newest");
        await AddAsync(host, other.Id, "Someone else's");
        var notifications = host.Get<INotificationService>();

        var unread = await notifications.GetUnreadAsync(user.Id, 2);

        CollectionAssert.AreEqual(new[] { "Newest", "Middle" }, unread.Select(n => n.Title).ToArray());
        Assert.AreEqual(3, await notifications.GetUnreadCountAsync(user.Id));
    }

    /// <summary>The list pages at 20, newest first, read ones included.</summary>
    [TestMethod]
    public async Task GetPageAsync_TwentyFiveNotifications_PagesAtTwenty()
    {
        await using var host = await TestHost.CreateAsync();
        var user = await host.CreateUserAsync("client@example.test", AppRoles.Client);
        for (var i = 0; i < 25; i++)
        {
            await AddAsync(host, user.Id, $"N{i}", minutesAgo: i, isRead: i % 2 == 0);
        }

        var notifications = host.Get<INotificationService>();
        var first = await notifications.GetPageAsync(user.Id, 1);
        var second = await notifications.GetPageAsync(user.Id, 2);

        Assert.AreEqual(25, first.TotalCount);
        Assert.HasCount(20, first.Items);
        Assert.AreEqual("N0", first.Items[0].Title);
        Assert.HasCount(5, second.Items);
    }

    /// <summary>Users can only mark their own notifications as read.</summary>
    [TestMethod]
    public async Task MarkAsReadAsync_OwnAndOthers_OnlyOwnIsMarked()
    {
        await using var host = await TestHost.CreateAsync();
        var user = await host.CreateUserAsync("client@example.test", AppRoles.Client);
        var other = await host.CreateUserAsync("other@example.test", AppRoles.Client);
        var mine = await AddAsync(host, user.Id, "Mine");
        var theirs = await AddAsync(host, other.Id, "Theirs");
        var notifications = host.Get<INotificationService>();

        Assert.IsTrue(await notifications.MarkAsReadAsync(user.Id, mine.NotificationId));
        Assert.IsFalse(await notifications.MarkAsReadAsync(user.Id, theirs.NotificationId));

        var stored = await host.Db.Notifications.AsNoTracking().ToListAsync();
        var myNotification = stored.Single(n => n.NotificationId == mine.NotificationId);
        Assert.IsTrue(myNotification.IsRead);
        Assert.AreEqual(host.Clock.Now, myNotification.ReadAt);
        Assert.IsFalse(stored.Single(n => n.NotificationId == theirs.NotificationId).IsRead);
    }

    /// <summary>Mark all as read clears the user's own unread notifications and nobody else's.</summary>
    [TestMethod]
    public async Task MarkAllAsReadAsync_SeveralUnread_MarksOnlyTheUsers()
    {
        await using var host = await TestHost.CreateAsync();
        var user = await host.CreateUserAsync("client@example.test", AppRoles.Client);
        var other = await host.CreateUserAsync("other@example.test", AppRoles.Client);
        await AddAsync(host, user.Id, "One");
        await AddAsync(host, user.Id, "Two");
        await AddAsync(host, user.Id, "Old", isRead: true);
        await AddAsync(host, other.Id, "Theirs");
        var notifications = host.Get<INotificationService>();

        Assert.AreEqual(2, await notifications.MarkAllAsReadAsync(user.Id));

        Assert.AreEqual(0, await notifications.GetUnreadCountAsync(user.Id));
        Assert.AreEqual(1, await notifications.GetUnreadCountAsync(other.Id));
    }

    /// <summary>UC06: saved settings come back on the form; a user without settings gets them created.</summary>
    [TestMethod]
    public async Task SavePreferencesAsync_ValidForm_SavesAndReturnsThem()
    {
        await using var host = await TestHost.CreateAsync();
        var user = await host.CreateUserAsync("client@example.test", AppRoles.Client);
        var notifications = host.Get<INotificationService>();
        var form = new ReminderPreferencesViewModel
        {
            WorkoutRemindersEnabled = false,
            WorkoutReminderTime = new TimeSpan(18, 30, 0),
            MealRemindersEnabled = true,
            MealReminderLeadMinutes = 0,
            AppointmentRemindersEnabled = true,
            AppointmentReminderLeadHours = 72,
            EmailEnabled = true
        };

        Assert.IsTrue(await notifications.SavePreferencesAsync(user.Id, form));

        var saved = (await notifications.GetPreferencesAsync(user.Id))!;
        Assert.IsFalse(saved.WorkoutRemindersEnabled);
        Assert.AreEqual(new TimeSpan(18, 30, 0), saved.WorkoutReminderTime);
        Assert.AreEqual(0, saved.MealReminderLeadMinutes);
        Assert.AreEqual(72, saved.AppointmentReminderLeadHours);
        Assert.IsTrue(saved.EmailEnabled);
        Assert.AreEqual("client@example.test", saved.Email);
        Assert.AreEqual(1, await host.Db.NotificationPreferences.CountAsync());
    }

    /// <summary>Spec 2.8: lead times outside 0–120 minutes and 1–72 hours are refused by the service too.</summary>
    [TestMethod]
    [DataRow(121, 24)]
    [DataRow(-1, 24)]
    [DataRow(15, 0)]
    [DataRow(15, 73)]
    public async Task SavePreferencesAsync_LeadTimeOutOfRange_ThrowsBr19(int mealMinutes, int appointmentHours)
    {
        await using var host = await TestHost.CreateAsync();
        var user = await host.CreateUserAsync("client@example.test", AppRoles.Client);
        var form = new ReminderPreferencesViewModel
        {
            WorkoutReminderTime = new TimeSpan(7, 0, 0),
            MealReminderLeadMinutes = mealMinutes,
            AppointmentReminderLeadHours = appointmentHours
        };

        await RuleAssert.BrokenAsync("BR-19", () => host.Get<INotificationService>().SavePreferencesAsync(user.Id, form));
        Assert.IsFalse(await host.Db.NotificationPreferences.AnyAsync());
    }

    /// <summary>Workout and meal reminders are offered to clients only; appointment reminders to clients and professionals.</summary>
    [TestMethod]
    public async Task GetPreferencesAsync_ByRole_ShowsTheSectionsThatApply()
    {
        await using var host = await TestHost.CreateAsync();
        var client = await host.CreateClientAsync("client@example.test");
        var trainer = await host.CreateTrainerAsync("trainer@example.test");
        var admin = await host.CreateUserAsync("admin@example.test", AppRoles.Admin);
        var notifications = host.Get<INotificationService>();

        var forClient = (await notifications.GetPreferencesAsync(client.UserId))!;
        var forTrainer = (await notifications.GetPreferencesAsync(trainer.UserId))!;
        var forAdmin = (await notifications.GetPreferencesAsync(admin.Id))!;

        Assert.IsTrue(forClient.ShowsPlanReminders);
        Assert.IsTrue(forClient.ShowsAppointmentReminders);
        Assert.IsFalse(forTrainer.ShowsPlanReminders);
        Assert.IsTrue(forTrainer.ShowsAppointmentReminders);
        Assert.IsFalse(forAdmin.ShowsPlanReminders);
        Assert.IsFalse(forAdmin.ShowsAppointmentReminders);
        Assert.AreEqual(24, forAdmin.AppointmentReminderLeadHours);
    }
}
