using System.Text;
using HealthTrack.Data;
using HealthTrack.Helpers;
using HealthTrack.Models.Entities;
using HealthTrack.Models.Enums;
using HealthTrack.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Services
{
    /// <summary>
    /// Saves in-app notifications, sends their email copies and keeps each user's reminder settings.
    /// </summary>
    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _db;
        private readonly IEmailService _email;
        private readonly IClock _clock;
        private readonly ILogger<NotificationService> _logger;

        /// <summary>
        /// Creates the service.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="email">Sends the email copies.</param>
        /// <param name="clock">Supplies the time.</param>
        /// <param name="logger">Records email failures.</param>
        public NotificationService(ApplicationDbContext db, IEmailService email, IClock clock, ILogger<NotificationService> logger)
        {
            _db = db;
            _email = email;
            _clock = clock;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<Notification> NotifyAsync(string userId, NotificationType type, string title, string message,
            string? linkUrl = null, string? reminderKey = null)
        {
            // A notification link is followed by the browser, so only local paths are allowed (no open redirects).
            if (linkUrl is not null && !IsLocalPath(linkUrl))
            {
                throw new ArgumentException("A notification link must be a local path of up to 200 characters, such as /Client/Appointments.", nameof(linkUrl));
            }

            var notification = new Notification
            {
                UserId = userId,
                Type = type,
                Title = title.Truncate(100)!,
                Message = message.Truncate(500)!,
                LinkUrl = linkUrl,
                ReminderKey = reminderKey,
                CreatedAt = _clock.Now
            };

            _db.Notifications.Add(notification);
            await _db.SaveChangesAsync();

            await SendEmailCopyAsync(notification);
            return notification;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<NotificationItemViewModel>> GetUnreadAsync(string userId, int take) =>
            await Items(_db.Notifications.Where(n => n.UserId == userId && !n.IsRead)).Take(take).ToListAsync();

        /// <inheritdoc />
        public Task<int> GetUnreadCountAsync(string userId) =>
            _db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);

        /// <inheritdoc />
        public Task<PagedResult<NotificationItemViewModel>> GetPageAsync(string userId, int page) =>
            Items(_db.Notifications.Where(n => n.UserId == userId)).ToPagedResultAsync(page);

        /// <inheritdoc />
        public async Task<bool> MarkAsReadAsync(string userId, int notificationId)
        {
            // Only the user's own notifications; anything else looks like it doesn't exist.
            var notification = await _db.Notifications.FirstOrDefaultAsync(n => n.NotificationId == notificationId && n.UserId == userId);
            if (notification is null)
            {
                return false;
            }

            notification.MarkAsRead(_clock.Now);
            await _db.SaveChangesAsync();
            return true;
        }

        /// <inheritdoc />
        public async Task<int> MarkAllAsReadAsync(string userId)
        {
            var unread = await _db.Notifications.Where(n => n.UserId == userId && !n.IsRead).ToListAsync();
            var now = _clock.Now;
            foreach (var notification in unread)
            {
                notification.MarkAsRead(now);
            }

            await _db.SaveChangesAsync();
            return unread.Count;
        }

        /// <inheritdoc />
        public async Task<ReminderPreferencesViewModel?> GetPreferencesAsync(string userId)
        {
            var user = await _db.Users.AsNoTracking()
                .Include(u => u.NotificationPreference)
                .FirstOrDefaultAsync(u => u.Id == userId);
            if (user is null)
            {
                return null;
            }

            // Every account gets its settings at registration; the defaults cover any account that somehow has none.
            var preference = user.NotificationPreference ?? NotificationPreference.CreateDefault(userId);
            var model = new ReminderPreferencesViewModel
            {
                WorkoutRemindersEnabled = preference.WorkoutRemindersEnabled,
                WorkoutReminderTime = preference.WorkoutReminderTime,
                MealRemindersEnabled = preference.MealRemindersEnabled,
                MealReminderLeadMinutes = preference.MealReminderLeadMinutes,
                AppointmentRemindersEnabled = preference.AppointmentRemindersEnabled,
                AppointmentReminderLeadHours = preference.AppointmentReminderLeadHours,
                EmailEnabled = preference.EmailEnabled
            };
            await FillPreferencesDisplayAsync(model, userId);
            return model;
        }

        /// <inheritdoc />
        public async Task FillPreferencesDisplayAsync(ReminderPreferencesViewModel model, string userId)
        {
            var user = await _db.Users.AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new { u.Email, IsClient = u.ClientProfile != null, IsTrainer = u.TrainerProfile != null })
                .FirstOrDefaultAsync();

            model.Email = user?.Email;
            model.ShowsPlanReminders = user?.IsClient == true;
            model.ShowsAppointmentReminders = user is { IsClient: true } or { IsTrainer: true };
        }

        /// <inheritdoc />
        public async Task<bool> SavePreferencesAsync(string userId, ReminderPreferencesViewModel model)
        {
            if (!await _db.Users.AnyAsync(u => u.Id == userId))
            {
                return false;
            }

            // Spec 2.8: the view model checks the ranges too; the service never trusts the form.
            var mealLead = model.MealReminderLeadMinutes ?? -1;
            if (mealLead is < NotificationPreference.MinMealLeadMinutes or > NotificationPreference.MaxMealLeadMinutes)
            {
                throw new BusinessRuleException("BR-19", "Meal reminders can come 0 to 120 minutes before the meal.");
            }

            var appointmentLead = model.AppointmentReminderLeadHours ?? -1;
            if (appointmentLead is < NotificationPreference.MinAppointmentLeadHours or > NotificationPreference.MaxAppointmentLeadHours)
            {
                throw new BusinessRuleException("BR-19", "Appointment reminders can come 1 to 72 hours before the appointment.");
            }

            if (model.WorkoutReminderTime is not { } workoutTime || workoutTime < TimeSpan.Zero || workoutTime >= TimeSpan.FromDays(1))
            {
                throw new BusinessRuleException("BR-19", "Enter a workout reminder time of day.");
            }

            var preference = await _db.NotificationPreferences.FirstOrDefaultAsync(p => p.UserId == userId);
            if (preference is null)
            {
                preference = NotificationPreference.CreateDefault(userId);
                _db.NotificationPreferences.Add(preference);
            }

            preference.WorkoutRemindersEnabled = model.WorkoutRemindersEnabled;
            // Whole minutes, like the time picker.
            preference.WorkoutReminderTime = new TimeSpan(workoutTime.Hours, workoutTime.Minutes, 0);
            preference.MealRemindersEnabled = model.MealRemindersEnabled;
            preference.MealReminderLeadMinutes = mealLead;
            preference.AppointmentRemindersEnabled = model.AppointmentRemindersEnabled;
            preference.AppointmentReminderLeadHours = appointmentLead;
            preference.EmailEnabled = model.EmailEnabled;
            await _db.SaveChangesAsync();
            return true;
        }

        // Spec 1.8: an email copy goes to active accounts that switched email copies on. The in-app notification is
        // already saved, so a mail failure is only logged (never the message, which can mention appointments).
        private async Task SendEmailCopyAsync(Notification notification)
        {
            var recipient = await _db.Users.AsNoTracking()
                .Where(u => u.Id == notification.UserId)
                .Select(u => new
                {
                    u.Email,
                    u.FirstName,
                    u.AccountStatus,
                    EmailEnabled = u.NotificationPreference != null && u.NotificationPreference.EmailEnabled
                })
                .FirstOrDefaultAsync();
            if (recipient is not { EmailEnabled: true, AccountStatus: AccountStatus.Active, Email: { Length: > 0 } address })
            {
                return;
            }

            try
            {
                await _email.SendAsync(address, "HealthTrack: " + notification.Title, EmailBody(recipient.FirstName, notification));
            }
            catch (Exception ex)
            {
                // Any failure of the mail system (no server, refused address, unwritable folder, time-out) is caught here:
                // the action that raised the notification has already succeeded and must not fail because of email.
                _logger.LogWarning(ex, "The email copy of notification {NotificationId} could not be sent.", notification.NotificationId);
                return;
            }

            notification.EmailSentAt = _clock.Now;
            await _db.SaveChangesAsync();
        }

        private static string EmailBody(string firstName, Notification notification)
        {
            var body = new StringBuilder()
                .Append("Hello ").Append(firstName).AppendLine(",")
                .AppendLine()
                .AppendLine(notification.Title)
                .AppendLine(notification.Message)
                .AppendLine()
                .AppendLine("Sign in to HealthTrack and open Notifications (the bell at the top of every page) to see it.")
                .AppendLine()
                .AppendLine("You get this email because email copies are on in your notification settings.");
            return body.ToString();
        }

        private static IQueryable<NotificationItemViewModel> Items(IQueryable<Notification> notifications) =>
            notifications.AsNoTracking()
                .OrderByDescending(n => n.CreatedAt)
                .ThenByDescending(n => n.NotificationId)
                .Select(n => new NotificationItemViewModel
                {
                    NotificationId = n.NotificationId,
                    Type = n.Type,
                    Title = n.Title,
                    Message = n.Message,
                    LinkUrl = n.LinkUrl,
                    IsRead = n.IsRead,
                    CreatedAt = n.CreatedAt
                });

        // The same rule as Url.IsLocalUrl for paths: starts with one "/", not "//" or "/\".
        private static bool IsLocalPath(string url) =>
            url.Length is > 0 and <= 200
            && url[0] == '/'
            && (url.Length == 1 || (url[1] != '/' && url[1] != '\\'));
    }
}
