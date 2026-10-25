using HealthTrack.Models.Entities;

namespace HealthTrack.Tests.Entities;

/// <summary>
/// Reading notifications and the default reminder settings (spec 2.8).
/// </summary>
[TestClass]
public sealed class NotificationTests
{
    /// <summary>Marking as read records the time.</summary>
    [TestMethod]
    public void MarkAsRead_Unread_SetsIsReadAndReadAt()
    {
        var notification = new Notification();
        var now = new DateTime(2026, 10, 25, 9, 15, 0);

        notification.MarkAsRead(now);

        Assert.IsTrue(notification.IsRead);
        Assert.AreEqual(now, notification.ReadAt);
    }

    /// <summary>Reading it again keeps the first read time.</summary>
    [TestMethod]
    public void MarkAsRead_AlreadyRead_KeepsFirstReadAt()
    {
        var notification = new Notification();
        var first = new DateTime(2026, 10, 25, 9, 15, 0);
        notification.MarkAsRead(first);

        notification.MarkAsRead(first.AddHours(2));

        Assert.AreEqual(first, notification.ReadAt);
    }

    /// <summary>Defaults: all reminders on, workout at 07:00, meals 15 minutes and appointments 24 hours ahead, no email.</summary>
    [TestMethod]
    public void CreateDefault_UserId_UsesSpecDefaults()
    {
        var preference = NotificationPreference.CreateDefault("user-1");

        Assert.AreEqual("user-1", preference.UserId);
        Assert.IsTrue(preference.WorkoutRemindersEnabled);
        Assert.AreEqual(new TimeSpan(7, 0, 0), preference.WorkoutReminderTime);
        Assert.IsTrue(preference.MealRemindersEnabled);
        Assert.AreEqual(15, preference.MealReminderLeadMinutes);
        Assert.IsTrue(preference.AppointmentRemindersEnabled);
        Assert.AreEqual(24, preference.AppointmentReminderLeadHours);
        Assert.IsFalse(preference.EmailEnabled);
    }
}
