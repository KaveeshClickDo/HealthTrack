using System.Globalization;
using HealthTrack.Data;
using HealthTrack.Helpers;
using HealthTrack.Models.Enums;
using HealthTrack.Models.ViewModels;
using HealthTrack.Security;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Services
{
    /// <summary>
    /// Builds the three dashboards (spec 1.4). Every query is read-only; figures that other pages show too come from the
    /// same services, so a dashboard never disagrees with the page it links to.
    /// </summary>
    public class DashboardService : IDashboardService
    {
        // Spec 1.4: the next 3 appointments, a handful of unread notifications, 14 days of logins, 10 audit events.
        private const int UpcomingAppointments = 3;
        private const int UnreadNotifications = 5;
        private const int LoginDays = 14;
        private const int RecentAuditEvents = 10;

        private readonly ApplicationDbContext _db;
        private readonly IAssignmentService _assignments;
        private readonly IAppointmentService _appointments;
        private readonly IDietService _diet;
        private readonly IFeedbackService _feedback;
        private readonly INotificationService _notifications;
        private readonly IAuditService _audit;
        private readonly IClock _clock;

        /// <summary>
        /// Creates the service.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="assignments">The trainer's clients and trainer workload.</param>
        /// <param name="appointments">Upcoming appointments and the trainer's requests.</param>
        /// <param name="diet">Today's meals and eating.</param>
        /// <param name="feedback">The latest feedback.</param>
        /// <param name="notifications">Unread notifications.</param>
        /// <param name="audit">The latest audit events.</param>
        /// <param name="clock">Supplies the time.</param>
        public DashboardService(ApplicationDbContext db, IAssignmentService assignments, IAppointmentService appointments, IDietService diet,
            IFeedbackService feedback, INotificationService notifications, IAuditService audit, IClock clock)
        {
            _db = db;
            _assignments = assignments;
            _appointments = appointments;
            _diet = diet;
            _feedback = feedback;
            _notifications = notifications;
            _audit = audit;
            _clock = clock;
        }

        /// <inheritdoc />
        public async Task<ClientDashboardViewModel?> GetClientDashboardAsync(string clientUserId)
        {
            var client = await _db.ClientProfiles.AsNoTracking()
                .Where(c => c.UserId == clientUserId)
                .Select(c => new
                {
                    c.ClientId,
                    c.User.FirstName,
                    c.TargetWeightKg,
                    TrainerName = c.Assignments
                        .Where(a => a.EndDate == null)
                        .Select(a => a.Trainer.User.FirstName + " " + a.Trainer.User.LastName)
                        .FirstOrDefault()
                })
                .FirstOrDefaultAsync();
            if (client is null)
            {
                return null;
            }

            var clientId = client.ClientId;
            var today = _clock.Today;
            var measurements = _db.HealthMeasurements.AsNoTracking().Where(m => m.ClientId == clientId);
            var first = await measurements.OrderBy(m => m.MeasuredAt).ThenBy(m => m.MeasurementId).FirstOrDefaultAsync();
            var latest = await measurements.OrderByDescending(m => m.MeasuredAt).ThenByDescending(m => m.MeasurementId).FirstOrDefaultAsync();

            // This week runs from Monday to today.
            var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
            var thisWeek = await _db.WorkoutLogs.AsNoTracking()
                .Where(l => l.ClientId == clientId && l.WorkoutDate >= monday && l.WorkoutDate <= today)
                .Select(l => l.Entries.Sum(e => e.CaloriesBurned))
                .ToListAsync();

            var feedback = await _feedback.GetForClientAsync(clientId, 1);
            return new ClientDashboardViewModel
            {
                FirstName = client.FirstName,
                TrainerName = client.TrainerName,
                LatestWeightKg = latest?.WeightKg,
                LatestMeasuredAt = latest?.MeasuredAt,
                LatestBmi = latest?.Bmi,
                BmiCategory = latest?.BmiCategory,
                WeightChangeKg = latest is not null && first is not null && first.MeasurementId != latest.MeasurementId
                    ? latest.WeightKg - first.WeightKg
                    : null,
                FirstMeasuredAt = first?.MeasuredAt,
                TargetWeightKg = client.TargetWeightKg,
                CaloriesBurnedThisWeek = thisWeek.Sum(),
                WorkoutsThisWeek = thisWeek.Count,
                TodaysWorkout = await TodaysWorkoutAsync(clientId, today),
                TodaysMeals = await _diet.GetTodaysMealsAsync(clientId, today),
                Nutrition = await _diet.GetDailySummaryAsync(clientId, today),
                UpcomingAppointments = await _appointments.GetUpcomingAsync(clientUserId, UpcomingAppointments),
                LatestFeedback = feedback.Items.FirstOrDefault(),
                UnreadNotifications = await _notifications.GetUnreadAsync(clientUserId, UnreadNotifications),
                UnreadCount = await _notifications.GetUnreadCountAsync(clientUserId)
            };
        }

        /// <inheritdoc />
        public async Task<TrainerDashboardViewModel?> GetTrainerDashboardAsync(string trainerUserId)
        {
            var firstName = await _db.TrainerProfiles.AsNoTracking()
                .Where(t => t.UserId == trainerUserId)
                .Select(t => t.User.FirstName)
                .FirstOrDefaultAsync();
            if (firstName is null)
            {
                return null;
            }

            var today = _clock.Today;
            var clients = await _assignments.GetTrainerClientsAsync(trainerUserId);
            var appointments = await _appointments.GetForTrainerAsync(trainerUserId, 1);

            return new TrainerDashboardViewModel
            {
                FirstName = firstName,
                Today = today,
                // Spec 1.4: clients who need a nudge come first.
                Clients = clients.OrderByDescending(c => c.IsInactive).ThenByDescending(c => c.DaysInactive).ThenBy(c => c.FullName).ToList(),
                PendingRequests = appointments.Pending,
                TodaysSchedule = appointments.Upcoming
                    .Where(a => a.StartDateTime.Date == today && a.Status is AppointmentStatus.Approved or AppointmentStatus.Rescheduled
                        or AppointmentStatus.Completed or AppointmentStatus.NoShow)
                    .ToList()
            };
        }

        /// <inheritdoc />
        public async Task<AdminDashboardViewModel> GetAdminDashboardAsync(string adminUserId)
        {
            var now = _clock.Now;
            var today = _clock.Today;
            var monthStart = new DateTime(today.Year, today.Month, 1);

            var accounts = await (
                    from user in _db.Users.AsNoTracking()
                    join userRole in _db.UserRoles on user.Id equals userRole.UserId
                    join role in _db.Roles on userRole.RoleId equals role.Id
                    select new { Role = role.Name, user.AccountStatus })
                .ToListAsync();

            var unassigned = await _db.ClientProfiles.AsNoTracking()
                .Where(c => c.User.AccountStatus == AccountStatus.Active && !c.Assignments.Any(a => a.EndDate == null))
                .OrderBy(c => c.CreatedAt)
                .Select(c => new UnassignedClientViewModel
                {
                    ClientId = c.ClientId,
                    FullName = c.User.FirstName + " " + c.User.LastName,
                    Email = c.User.Email ?? string.Empty,
                    JoinedAt = c.CreatedAt
                })
                .ToListAsync();

            var auditPage = await _audit.GetPageAsync(new AuditLogFilterViewModel(), 1);
            return new AdminDashboardViewModel
            {
                FirstName = await _db.Users.AsNoTracking().Where(u => u.Id == adminUserId).Select(u => u.FirstName).FirstOrDefaultAsync() ?? string.Empty,
                UsersByRole = AppRoles.All.Select(role => new RoleStatusCountViewModel
                {
                    Role = role,
                    Active = accounts.Count(a => a.Role == role && a.AccountStatus == AccountStatus.Active),
                    PendingApproval = accounts.Count(a => a.Role == role && a.AccountStatus == AccountStatus.PendingApproval),
                    Suspended = accounts.Count(a => a.Role == role && a.AccountStatus == AccountStatus.Suspended),
                    Deactivated = accounts.Count(a => a.Role == role && a.AccountStatus == AccountStatus.Deactivated)
                }).ToList(),
                NewRegistrationsThisMonth = await _db.Users.CountAsync(u => u.CreatedAt >= monthStart),
                ActiveUsersLast7Days = await _db.Users.CountAsync(u => u.LastLoginAt >= now.AddDays(-7)),
                TrainersAwaitingApproval = await _db.TrainerProfiles.CountAsync(t => t.User.AccountStatus == AccountStatus.PendingApproval),
                UnassignedClients = unassigned,
                TrainerLoad = await _assignments.GetTrainerLoadAsync(),
                LoginsPerDay = await LoginsPerDayAsync(today),
                AppointmentsByStatus = await AppointmentsByStatusAsync(),
                RecentAuditEvents = auditPage.Items.Take(RecentAuditEvents).ToList()
            };
        }

        // The active plan's exercises for today and whether a session is logged (the client's "Start" button).
        private async Task<TodayWorkoutViewModel> TodaysWorkoutAsync(int clientId, DateTime today)
        {
            var plan = await _db.WorkoutPlans.AsNoTracking()
                .Include(p => p.Exercises).ThenInclude(e => e.Exercise)
                .Where(p => p.ClientId == clientId && p.Status == PlanStatus.Active
                    && p.StartDate <= today && (p.EndDate == null || p.EndDate >= today))
                .OrderByDescending(p => p.StartDate)
                .FirstOrDefaultAsync();

            return new TodayWorkoutViewModel
            {
                HasPlan = plan is not null,
                PlanTitle = plan?.Title,
                Exercises = plan?.GetExercisesFor(today.DayOfWeek)
                    .Select(l => $"{l.Exercise.Name} – {WorkoutText.Prescription(l.Sets, l.Reps, l.DurationMinutes, l.RestSeconds)}")
                    .ToList() ?? [],
                LoggedToday = await _db.WorkoutLogs.AnyAsync(l => l.ClientId == clientId && l.WorkoutDate == today)
            };
        }

        private async Task<ChartSeries> LoginsPerDayAsync(DateTime today)
        {
            var first = today.AddDays(1 - LoginDays);
            var logins = (await _db.AuditLogs.AsNoTracking()
                    .Where(a => a.Action == AuditAction.Login && a.Timestamp >= first)
                    .Select(a => a.Timestamp)
                    .ToListAsync())
                .GroupBy(t => t.Date)
                .ToDictionary(g => g.Key, g => g.Count());
            var days = Enumerable.Range(0, LoginDays).Select(i => first.AddDays(i)).ToList();

            return new ChartSeries
            {
                Labels = days.Select(DateLabel).ToList(),
                Datasets = [new ChartDataset { Label = "Logins", Data = days.Select(d => (decimal?)logins.GetValueOrDefault(d)).ToList() }]
            };
        }

        private async Task<ChartSeries> AppointmentsByStatusAsync()
        {
            var counts = await _db.Appointments.AsNoTracking()
                .GroupBy(a => a.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.Status, g => g.Count);
            var statuses = Enum.GetValues<AppointmentStatus>();

            return new ChartSeries
            {
                Labels = statuses.Select(s => s.GetDisplayName()).ToList(),
                Datasets = [new ChartDataset { Label = "Appointments", Type = "bar", Data = statuses.Select(s => (decimal?)counts.GetValueOrDefault(s)).ToList() }]
            };
        }

        private static string DateLabel(DateTime date) => date.ToString("dd MMM yyyy", CultureInfo.CurrentCulture);
    }
}
