using HealthTrack.Models.Entities;
using HealthTrack.Models.Enums;
using HealthTrack.Models.ViewModels;

namespace HealthTrack.Services
{
    /// <summary>
    /// In-app notifications, their email copies and the user's reminder settings (spec 1.8, UC06). The service that makes
    /// a change raises its notifications through this.
    /// </summary>
    public interface INotificationService
    {
        /// <summary>
        /// Saves a notification for a user. When the user has email copies on, the notification is marked EmailPending and
        /// <see cref="SendPendingEmailsAsync"/> emails it after the change commits; mail never runs inside the caller's
        /// transaction and a mail failure never breaks the action that raised the notification.
        /// </summary>
        /// <param name="userId">The recipient.</param>
        /// <param name="type">The kind of notification.</param>
        /// <param name="title">A short title, cut to 100 characters.</param>
        /// <param name="message">The message, cut to 500 characters.</param>
        /// <param name="linkUrl">A local path such as /Client/Appointments; never an absolute URL.</param>
        /// <param name="reminderKey">For reminders only: the unique key that stops duplicates (BR-19), such as MEAL-42-20261025.</param>
        /// <returns>The saved notification.</returns>
        /// <exception cref="ArgumentException"><paramref name="linkUrl"/> is not a local path of up to 200 characters.</exception>
        Task<Notification> NotifyAsync(string userId, NotificationType type, string title, string message, string? linkUrl = null,
            string? reminderKey = null);

        /// <summary>
        /// Emails the pending copies (run by <see cref="ReminderBackgroundService"/> every minute): up to 100 a run, oldest
        /// first. A copy whose recipient has since switched copies off or been suspended is dropped; when the mail system
        /// fails, the rest wait for the next run; a copy still unsent after 24 hours is given up.
        /// </summary>
        /// <param name="now">The current time.</param>
        /// <returns>How many copies were sent.</returns>
        Task<int> SendPendingEmailsAsync(DateTime now);

        /// <summary>
        /// The user's newest unread notifications, for the bell.
        /// </summary>
        /// <param name="userId">The signed-in user.</param>
        /// <param name="take">How many to return.</param>
        /// <returns>Up to <paramref name="take"/> unread notifications, newest first.</returns>
        Task<IReadOnlyList<NotificationItemViewModel>> GetUnreadAsync(string userId, int take);

        /// <summary>
        /// How many unread notifications the user has, for the bell's badge.
        /// </summary>
        /// <param name="userId">The signed-in user.</param>
        /// <returns>The count.</returns>
        Task<int> GetUnreadCountAsync(string userId);

        /// <summary>
        /// One page (20 rows) of the user's notifications, newest first, read and unread.
        /// </summary>
        /// <param name="userId">The signed-in user.</param>
        /// <param name="page">The 1-based page.</param>
        /// <returns>The page.</returns>
        Task<PagedResult<NotificationItemViewModel>> GetPageAsync(string userId, int page);

        /// <summary>
        /// Marks one of the user's own notifications as read.
        /// </summary>
        /// <param name="userId">The signed-in user.</param>
        /// <param name="notificationId">The notification.</param>
        /// <returns>False when it doesn't exist or belongs to someone else.</returns>
        Task<bool> MarkAsReadAsync(string userId, int notificationId);

        /// <summary>
        /// Marks all the user's notifications as read.
        /// </summary>
        /// <param name="userId">The signed-in user.</param>
        /// <returns>How many were unread.</returns>
        Task<int> MarkAllAsReadAsync(string userId);

        /// <summary>
        /// The user's reminder settings for the form; the spec defaults when none are saved yet.
        /// </summary>
        /// <param name="userId">The signed-in user.</param>
        /// <returns>The form, or null for an unknown user.</returns>
        Task<ReminderPreferencesViewModel?> GetPreferencesAsync(string userId);

        /// <summary>
        /// Fills the form's display values (the address and which sections apply) after a failed post.
        /// </summary>
        /// <param name="model">The posted form.</param>
        /// <param name="userId">The signed-in user.</param>
        /// <returns>A task that completes when the values are set.</returns>
        Task FillPreferencesDisplayAsync(ReminderPreferencesViewModel model, string userId);

        /// <summary>
        /// Saves the user's reminder settings, checking the ranges of spec 2.8 again.
        /// </summary>
        /// <param name="userId">The signed-in user.</param>
        /// <param name="model">The form.</param>
        /// <returns>False for an unknown user.</returns>
        /// <exception cref="Helpers.BusinessRuleException">A lead time is outside its range (BR-19).</exception>
        Task<bool> SavePreferencesAsync(string userId, ReminderPreferencesViewModel model);
    }
}
