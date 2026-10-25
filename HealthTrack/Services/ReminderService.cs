using System.Globalization;
using HealthTrack.Data;
using HealthTrack.Helpers;
using HealthTrack.Models.Entities;
using HealthTrack.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Services
{
    /// <summary>
    /// Finds the workout, meal and appointment reminders that are due and creates each one once (spec 1.8, BR-19). Every
    /// reminder carries a unique ReminderKey, so running the check every minute, on every poll and on every dashboard
    /// visit never repeats one.
    /// </summary>
    public class ReminderService : IReminderService
    {
        // The background service has no request culture, so dates in messages use the app's culture explicitly.
        private static readonly CultureInfo AppCulture = CultureInfo.GetCultureInfo("en-GB");

        private readonly ApplicationDbContext _db;
        private readonly INotificationService _notifications;

        /// <summary>
        /// Creates the service.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="notifications">Saves the reminders (and sends their email copies).</param>
        public ReminderService(ApplicationDbContext db, INotificationService notifications)
        {
            _db = db;
            _notifications = notifications;
        }

        /// <inheritdoc />
        public Task<int> GenerateDueRemindersAsync(string userId, DateTime now) => GenerateAsync(userId, now);

        /// <inheritdoc />
        public Task<int> GenerateAllDueRemindersAsync(DateTime now) => GenerateAsync(null, now);

        // One user, or everyone when userId is null.
        private async Task<int> GenerateAsync(string? userId, DateTime now)
        {
            var due = new List<DueReminder>();
            due.AddRange(await DueWorkoutRemindersAsync(userId, now));
            due.AddRange(await DueMealRemindersAsync(userId, now));
            due.AddRange(await DueAppointmentRemindersAsync(userId, now));
            if (due.Count == 0)
            {
                return 0;
            }

            // BR-19: skip the keys that already exist...
            var keys = due.Select(r => r.Key).ToList();
            var existing = (await _db.Notifications.AsNoTracking()
                    .Where(n => n.ReminderKey != null && keys.Contains(n.ReminderKey))
                    .Select(n => n.ReminderKey!)
                    .ToListAsync())
                .ToHashSet();

            var created = 0;
            foreach (var reminder in due.Where(r => !existing.Contains(r.Key)))
            {
                if (await CreateAsync(reminder))
                {
                    created++;
                }
            }

            return created;
        }

        // ...and let the unique index settle a race: a poll and the background service may check the same key at once.
        private async Task<bool> CreateAsync(DueReminder reminder)
        {
            try
            {
                await _notifications.NotifyAsync(reminder.UserId, reminder.Type, reminder.Title, reminder.Message, reminder.LinkUrl, reminder.Key);
                return true;
            }
            catch (DbUpdateException ex) when (SqlErrors.IsDuplicateKey(ex))
            {
                // The other one saved it first. Forget the rejected row, so the next SaveChanges doesn't try it again.
                foreach (var entry in _db.ChangeTracker.Entries<Notification>().Where(e => e.State == EntityState.Added).ToList())
                {
                    entry.State = EntityState.Detached;
                }

                return false;
            }
        }

        // Workout: today is a training day of the client's active plan, the reminder time has passed and nothing is
        // logged today. Key WORKOUT-{clientId}-{yyyyMMdd}.
        private async Task<List<DueReminder>> DueWorkoutRemindersAsync(string? userId, DateTime now)
        {
            var today = now.Date;
            var day = today.DayOfWeek;
            var plans = (await _db.WorkoutPlans.AsNoTracking()
                    .Where(p => p.ClientId != null && p.Status == PlanStatus.Active
                        && p.StartDate <= today && (p.EndDate == null || p.EndDate >= today)
                        && p.Exercises.Any(e => e.DayOfWeek == day)
                        && p.Client!.User.AccountStatus == AccountStatus.Active
                        && (userId == null || p.Client.UserId == userId))
                    .Select(p => new
                    {
                        ClientId = p.ClientId!.Value,
                        p.Client!.UserId,
                        p.Title,
                        Preference = p.Client.User.NotificationPreference
                    })
                    .ToListAsync())
                .Where(p => SettingsOf(p.Preference, p.UserId) is { WorkoutRemindersEnabled: true } settings
                    && now.TimeOfDay >= settings.WorkoutReminderTime)
                // BR-09 allows one active plan; this guards against two.
                .DistinctBy(p => p.ClientId)
                .ToList();
            if (plans.Count == 0)
            {
                return [];
            }

            var clientIds = plans.Select(p => p.ClientId).ToList();
            var loggedToday = (await _db.WorkoutLogs.AsNoTracking()
                    .Where(l => clientIds.Contains(l.ClientId) && l.WorkoutDate == today)
                    .Select(l => l.ClientId)
                    .ToListAsync())
                .ToHashSet();

            return plans
                .Where(p => !loggedToday.Contains(p.ClientId))
                .Select(p => new DueReminder(p.UserId, NotificationType.WorkoutReminder, "Time for today's workout",
                    $"Today is a training day in \"{p.Title}\". Log your workout once you've done it.",
                    "/Client/Workouts/Today",
                    $"WORKOUT-{Key(p.ClientId)}-{today.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}"))
                .ToList();
        }

        // Meal: a planned meal of the active diet plan applies today, now is within the lead time before it, and no meal
        // for that plan meal (or of that meal type) is logged today. Key MEAL-{dietPlanMealId}-{yyyyMMdd}.
        private async Task<List<DueReminder>> DueMealRemindersAsync(string? userId, DateTime now)
        {
            var today = now.Date;
            DayOfWeek? day = today.DayOfWeek;
            // Whole minutes, so a lead time of 0 still matches during the meal's own minute.
            var minute = today + new TimeSpan(now.Hour, now.Minute, 0);

            var meals = (await _db.DietPlanMeals.AsNoTracking()
                    .Where(m => m.DietPlan.Status == PlanStatus.Active
                        && m.DietPlan.StartDate <= today && (m.DietPlan.EndDate == null || m.DietPlan.EndDate >= today)
                        && (m.DayOfWeek == null || m.DayOfWeek == day)
                        && m.DietPlan.Client.User.AccountStatus == AccountStatus.Active
                        && (userId == null || m.DietPlan.Client.UserId == userId))
                    .Select(m => new
                    {
                        m.DietPlanMealId,
                        m.MealType,
                        m.ScheduledTime,
                        m.MealDescription,
                        m.DietPlan.ClientId,
                        m.DietPlan.Client.UserId,
                        Preference = m.DietPlan.Client.User.NotificationPreference
                    })
                    .ToListAsync())
                .Where(m => SettingsOf(m.Preference, m.UserId) is { MealRemindersEnabled: true } settings
                    && today + m.ScheduledTime - TimeSpan.FromMinutes(settings.MealReminderLeadMinutes) <= minute
                    && minute <= today + m.ScheduledTime)
                .ToList();
            if (meals.Count == 0)
            {
                return [];
            }

            var clientIds = meals.Select(m => m.ClientId).Distinct().ToList();
            var logged = await _db.MealLogs.AsNoTracking()
                .Where(l => clientIds.Contains(l.ClientId) && l.MealDate == today)
                .Select(l => new { l.ClientId, l.DietPlanMealId, l.MealType })
                .ToListAsync();

            return meals
                .Where(m => !logged.Any(l => l.DietPlanMealId == m.DietPlanMealId || (l.ClientId == m.ClientId && l.MealType == m.MealType)))
                .Select(m => new DueReminder(m.UserId, NotificationType.MealReminder,
                    $"{m.MealType.GetDisplayName()} at {Time(m.ScheduledTime)}",
                    $"Coming up: {m.MealDescription}. Log it once you've eaten.",
                    "/Client/Diet/LogMeal?dietPlanMealId=" + Key(m.DietPlanMealId),
                    $"MEAL-{Key(m.DietPlanMealId)}-{today.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}"))
                .ToList();
        }

        // Appointment: approved or rescheduled, not started, and starting within each person's lead time; one reminder for
        // the client (C) and one for the professional (T). The start time is part of the key, so a rescheduled
        // appointment gets a fresh reminder. Key APPT-{appointmentId}-{C or T}-{start yyyyMMddHHmm}.
        private async Task<List<DueReminder>> DueAppointmentRemindersAsync(string? userId, DateTime now)
        {
            var horizon = now.AddHours(NotificationPreference.MaxAppointmentLeadHours);
            var appointments = await _db.Appointments.AsNoTracking()
                .Where(a => (a.Status == AppointmentStatus.Approved || a.Status == AppointmentStatus.Rescheduled)
                    && a.StartDateTime > now && a.StartDateTime <= horizon
                    && (userId == null || a.Client.UserId == userId || a.Trainer.UserId == userId))
                .Select(a => new
                {
                    a.AppointmentId,
                    a.StartDateTime,
                    a.AppointmentType,
                    ClientUserId = a.Client.UserId,
                    ClientName = a.Client.User.FirstName + " " + a.Client.User.LastName,
                    ClientStatus = a.Client.User.AccountStatus,
                    ClientPreference = a.Client.User.NotificationPreference,
                    TrainerUserId = a.Trainer.UserId,
                    TrainerName = a.Trainer.User.FirstName + " " + a.Trainer.User.LastName,
                    TrainerStatus = a.Trainer.User.AccountStatus,
                    TrainerPreference = a.Trainer.User.NotificationPreference
                })
                .ToListAsync();

            var due = new List<DueReminder>();
            foreach (var a in appointments)
            {
                var what = $"{a.AppointmentType.GetDisplayName().ToLowerInvariant()} on "
                    + a.StartDateTime.ToString("dd MMM yyyy 'at' HH:mm", AppCulture);
                var start = a.StartDateTime.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture);

                // Only the user asked about; everyone in the background run.
                if (userId is null || userId == a.ClientUserId)
                {
                    AddIfDue(a.ClientUserId, a.ClientStatus, a.ClientPreference, a.StartDateTime,
                        $"Your {what} with {a.TrainerName}.", "/Client/Appointments/Details/" + Key(a.AppointmentId),
                        $"APPT-{Key(a.AppointmentId)}-C-{start}");
                }

                if (userId is null || userId == a.TrainerUserId)
                {
                    AddIfDue(a.TrainerUserId, a.TrainerStatus, a.TrainerPreference, a.StartDateTime,
                        $"Your {what} with {a.ClientName}.", "/Trainer/Appointments/Details/" + Key(a.AppointmentId),
                        $"APPT-{Key(a.AppointmentId)}-T-{start}");
                }
            }

            return due;

            void AddIfDue(string recipient, AccountStatus status, NotificationPreference? preference, DateTime startsAt, string message,
                string link, string key)
            {
                var settings = SettingsOf(preference, recipient);
                if (status == AccountStatus.Active && settings.AppointmentRemindersEnabled
                    && startsAt - now <= TimeSpan.FromHours(settings.AppointmentReminderLeadHours))
                {
                    due.Add(new DueReminder(recipient, NotificationType.AppointmentReminder, "Appointment coming up", message, link, key));
                }
            }
        }

        // Every account gets settings at registration; the spec defaults cover any account that somehow has none.
        private static NotificationPreference SettingsOf(NotificationPreference? preference, string userId) =>
            preference ?? NotificationPreference.CreateDefault(userId);

        private static string Time(TimeSpan time) => time.ToString(@"hh\:mm", CultureInfo.InvariantCulture);

        private static string Key(int id) => id.ToString(CultureInfo.InvariantCulture);
    }
}
