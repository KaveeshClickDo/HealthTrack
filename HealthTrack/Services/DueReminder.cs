using HealthTrack.Models.Enums;

namespace HealthTrack.Services
{
    /// <summary>
    /// A reminder that <see cref="ReminderService"/> found due, before it is saved as a notification.
    /// </summary>
    /// <param name="UserId">The recipient.</param>
    /// <param name="Type">Workout, meal or appointment reminder.</param>
    /// <param name="Title">The notification title.</param>
    /// <param name="Message">The notification message.</param>
    /// <param name="LinkUrl">The local page it opens.</param>
    /// <param name="Key">The unique ReminderKey, such as MEAL-42-20261025 (BR-19).</param>
    public sealed record DueReminder(string UserId, NotificationType Type, string Title, string Message, string LinkUrl, string Key);
}
