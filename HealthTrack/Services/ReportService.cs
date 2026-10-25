using System.Globalization;
using HealthTrack.Data;
using HealthTrack.Helpers;
using HealthTrack.Models.Entities;
using HealthTrack.Models.Enums;
using HealthTrack.Models.ViewModels;
using HealthTrack.Models.ViewModels.Reports;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Services
{
    /// <summary>
    /// The reports of spec 1.7 and their CSV export. A trainer's scope is the clients actively assigned to them and their own
    /// appointments (BR-08); without a trainer the scope is everyone. All queries are read-only.
    /// </summary>
    public class ReportService : IReportService
    {
        /// <summary>The client performance report's key, as used in export links and file names.</summary>
        public const string ClientPerformance = "client-performance";

        /// <summary>The popular routines report's key.</summary>
        public const string PopularRoutines = "popular-routines";

        /// <summary>The engagement report's key.</summary>
        public const string Engagement = "engagement";

        /// <summary>The trainer workload report's key (administrators only).</summary>
        public const string TrainerWorkload = "trainer-workload";

        // Spec 1.7: the top 10 exercises; a client is active with any log in the last 7 days.
        private const int TopExerciseCount = 10;
        private const int ActiveWindowDays = 7;

        private readonly ApplicationDbContext _db;
        private readonly IAuditService _audit;
        private readonly IClock _clock;

        /// <summary>
        /// Creates the service.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="audit">Records exports.</param>
        /// <param name="clock">Supplies today's date.</param>
        public ReportService(ApplicationDbContext db, IAuditService audit, IClock clock)
        {
            _db = db;
            _audit = audit;
            _clock = clock;
        }

        /// <inheritdoc />
        public async Task<ClientPerformanceReportViewModel> GetClientPerformanceAsync(ReportRangeViewModel range, string? trainerUserId = null)
        {
            var ids = await ClientsInScopeAsync(trainerUserId);
            var start = range.From;
            var end = range.To.AddDays(1);

            var clients = await _db.ClientProfiles.AsNoTracking()
                .Where(c => ids.Contains(c.ClientId))
                .Select(c => new
                {
                    c.ClientId,
                    Name = c.User.FirstName + " " + c.User.LastName,
                    TrainerName = c.Assignments
                        .Where(a => a.EndDate == null)
                        .Select(a => a.Trainer.User.FirstName + " " + a.Trainer.User.LastName)
                        .FirstOrDefault()
                })
                .ToListAsync();
            var readings = await _db.HealthMeasurements.AsNoTracking()
                .Where(m => ids.Contains(m.ClientId) && m.MeasuredAt < end)
                .Select(m => new { m.ClientId, m.MeasuredAt, m.MeasurementId, m.WeightKg, m.HeightCm })
                .ToListAsync();
            var workouts = await _db.WorkoutLogs.AsNoTracking()
                .Where(l => ids.Contains(l.ClientId) && l.WorkoutDate >= start && l.WorkoutDate <= range.To)
                .Select(l => new { l.ClientId, Calories = l.Entries.Sum(e => e.CaloriesBurned) })
                .ToListAsync();
            var meals = await _db.MealLogs.AsNoTracking()
                .Where(l => ids.Contains(l.ClientId) && l.MealDate >= start && l.MealDate <= range.To)
                .Select(l => new { l.ClientId, l.MealDate, Calories = l.Items.Sum(i => i.Calories) })
                .ToListAsync();
            var dietPlans = await _db.DietPlans.AsNoTracking()
                .Where(p => ids.Contains(p.ClientId) && (p.Status == PlanStatus.Active || p.Status == PlanStatus.Completed))
                .ToListAsync();
            // BR-08: a trainer counts only the appointments they held themselves.
            var attended = await _db.Appointments.AsNoTracking()
                .Where(a => ids.Contains(a.ClientId) && a.Status == AppointmentStatus.Completed
                    && a.StartDateTime >= start && a.StartDateTime < end
                    && (trainerUserId == null || a.Trainer.UserId == trainerUserId))
                .Select(a => a.ClientId)
                .ToListAsync();
            var adherence = await WorkoutAdherence.ForClientsAsync(_db, ids, range.From, range.To, _clock.Today);

            var rows = clients.OrderBy(c => c.Name).Select(c =>
            {
                var mine = readings.Where(r => r.ClientId == c.ClientId).OrderBy(r => r.MeasuredAt).ThenBy(r => r.MeasurementId).ToList();
                // The weight at the start of the range: the last reading before it, else the first inside it.
                var first = mine.LastOrDefault(r => r.MeasuredAt < start) ?? mine.FirstOrDefault();
                var latest = mine.LastOrDefault();

                // Average eating over the days with logged meals, against the plan's target on those same days.
                var days = meals.Where(m => m.ClientId == c.ClientId).GroupBy(m => m.MealDate.Date).ToList();
                var plans = dietPlans.Where(p => p.ClientId == c.ClientId).ToList();
                var targets = days.Select(d => NutritionDays.PlanOn(plans, d.Key)?.DailyCalorieTarget).OfType<int>().ToList();

                return new ClientPerformanceRow
                {
                    ClientId = c.ClientId,
                    ClientName = c.Name,
                    TrainerName = c.TrainerName,
                    StartWeightKg = first?.WeightKg,
                    LatestWeightKg = latest?.WeightKg,
                    StartBmi = first is null ? null : HealthCalculator.CalculateBmi(first.WeightKg, first.HeightCm),
                    LatestBmi = latest is null ? null : HealthCalculator.CalculateBmi(latest.WeightKg, latest.HeightCm),
                    WorkoutsCompleted = workouts.Count(w => w.ClientId == c.ClientId),
                    AdherencePercent = adherence[c.ClientId].Percent,
                    AverageDailyCalories = days.Count == 0 ? null : Round0(days.Sum(d => d.Sum(m => m.Calories)) / days.Count),
                    AverageDailyTarget = targets.Count == 0 ? null : Round0((decimal)targets.Average()),
                    CaloriesBurned = Round0(workouts.Where(w => w.ClientId == c.ClientId).Sum(w => w.Calories)),
                    AppointmentsAttended = attended.Count(id => id == c.ClientId)
                };
            }).ToList();

            return new ClientPerformanceReportViewModel
            {
                Range = range,
                AllClients = trainerUserId is null,
                Rows = rows,
                Chart = new ChartSeries
                {
                    Labels = rows.Select(r => r.ClientName).ToList(),
                    Datasets = [new ChartDataset { Label = "Workout adherence (%)", Type = "bar", Data = rows.Select(r => (decimal?)r.AdherencePercent).ToList() }]
                }
            };
        }

        /// <inheritdoc />
        public async Task<PopularRoutinesReportViewModel> GetPopularRoutinesAsync(ReportRangeViewModel range, string? trainerUserId = null)
        {
            var ids = await ClientsInScopeAsync(trainerUserId);

            // Which template each client plan was copied from (BR-12), and the plan in effect on every scheduled day.
            var copies = await _db.WorkoutPlans.AsNoTracking()
                .Where(p => p.SourceTemplateId != null && p.ClientId != null && ids.Contains(p.ClientId.Value))
                .Select(p => new { p.WorkoutPlanId, TemplateId = p.SourceTemplateId!.Value, p.Status })
                .ToListAsync();
            var templateOf = copies.ToDictionary(c => c.WorkoutPlanId, c => c.TemplateId);
            var days = (await WorkoutAdherence.ScheduledDaysAsync(_db, ids, range.From, range.To, _clock.Today))
                .Where(d => templateOf.ContainsKey(d.WorkoutPlanId))
                .ToList();

            var usedTemplateIds = copies.Select(c => c.TemplateId).Distinct().ToList();
            var templates = await _db.WorkoutPlans.AsNoTracking()
                .Where(p => p.IsTemplate && (trainerUserId == null || p.Trainer.UserId == trainerUserId || usedTemplateIds.Contains(p.WorkoutPlanId)))
                .Select(p => new
                {
                    p.WorkoutPlanId,
                    p.Title,
                    p.Difficulty,
                    Author = p.Trainer.User.FirstName + " " + p.Trainer.User.LastName
                })
                .ToListAsync();

            var routines = templates
                .Select(t =>
                {
                    var templateDays = days.Where(d => templateOf[d.WorkoutPlanId] == t.WorkoutPlanId).ToList();
                    return new RoutineUsageRow
                    {
                        TemplateId = t.WorkoutPlanId,
                        Title = t.Title,
                        Difficulty = t.Difficulty,
                        AuthorName = t.Author,
                        ClientsAssigned = templateDays.Select(d => d.ClientId).Distinct().Count(),
                        ActivePlans = copies.Count(c => c.TemplateId == t.WorkoutPlanId && c.Status == PlanStatus.Active),
                        AdherencePercent = WorkoutAdherence.Summarise(templateDays).Percent
                    };
                })
                .OrderByDescending(r => r.ClientsAssigned)
                .ThenByDescending(r => r.ActivePlans)
                .ThenByDescending(r => r.AdherencePercent ?? -1)
                .ThenBy(r => r.Title)
                .ToList();

            var entries = await _db.WorkoutLogEntries.AsNoTracking()
                .Where(e => ids.Contains(e.WorkoutLog.ClientId) && e.WorkoutLog.WorkoutDate >= range.From && e.WorkoutLog.WorkoutDate <= range.To)
                .Select(e => new { e.ExerciseId, e.Exercise.Name, e.Exercise.Category, e.DurationMinutes, e.CaloriesBurned })
                .ToListAsync();
            var topExercises = entries
                .GroupBy(e => new { e.ExerciseId, e.Name, e.Category })
                .Select(g => new ExerciseUsageRow
                {
                    ExerciseName = g.Key.Name,
                    Category = g.Key.Category,
                    TimesLogged = g.Count(),
                    TotalMinutes = g.Sum(e => e.DurationMinutes),
                    CaloriesBurned = Round0(g.Sum(e => e.CaloriesBurned))
                })
                .OrderByDescending(e => e.TimesLogged)
                .ThenBy(e => e.ExerciseName)
                .Take(TopExerciseCount)
                .ToList();
            var categories = entries
                .GroupBy(e => e.Category)
                .Select(g => new { Category = g.Key, Count = g.Count() })
                .OrderByDescending(c => c.Count)
                .ThenBy(c => c.Category)
                .Select(c => new CountRow { Label = c.Category.GetDisplayName(), Count = c.Count, Percent = Percent(c.Count, entries.Count) })
                .ToList();

            return new PopularRoutinesReportViewModel
            {
                Range = range,
                AllClients = trainerUserId is null,
                Routines = routines,
                TopExercises = topExercises,
                Categories = categories,
                Chart = new ChartSeries
                {
                    Labels = topExercises.Select(e => e.ExerciseName).ToList(),
                    Datasets = [new ChartDataset { Label = "Times logged", Type = "bar", Data = topExercises.Select(e => (decimal?)e.TimesLogged).ToList() }]
                }
            };
        }

        /// <inheritdoc />
        public async Task<EngagementReportViewModel> GetEngagementAsync(ReportRangeViewModel range, string? trainerUserId = null)
        {
            var ids = await ClientsInScopeAsync(trainerUserId);
            var start = range.From;
            var end = range.To.AddDays(1);

            // Every log counts as activity: workouts, meals and measurements.
            var workouts = await _db.WorkoutLogs.AsNoTracking()
                .Where(l => ids.Contains(l.ClientId) && l.WorkoutDate >= start && l.WorkoutDate <= range.To)
                .Select(l => new { l.ClientId, Date = l.WorkoutDate })
                .ToListAsync();
            var meals = await _db.MealLogs.AsNoTracking()
                .Where(l => ids.Contains(l.ClientId) && l.MealDate >= start && l.MealDate <= range.To)
                .Select(l => new { l.ClientId, Date = l.MealDate })
                .ToListAsync();
            var measurements = (await _db.HealthMeasurements.AsNoTracking()
                    .Where(m => ids.Contains(m.ClientId) && m.MeasuredAt >= start && m.MeasuredAt < end)
                    .Select(m => new { m.ClientId, m.MeasuredAt })
                    .ToListAsync())
                .Select(m => new { m.ClientId, Date = m.MeasuredAt.Date })
                .ToList();
            var activity = workouts.Concat(meals).Concat(measurements).ToList();

            // A trainer sees their clients' logins and registrations; an administrator sees everyone's.
            var clientUserIds = await _db.ClientProfiles.AsNoTracking().Where(c => ids.Contains(c.ClientId)).Select(c => c.UserId).ToListAsync();
            var logins = (await _db.AuditLogs.AsNoTracking()
                    .Where(a => a.Action == AuditAction.Login && a.Timestamp >= start && a.Timestamp < end
                        && (trainerUserId == null || (a.UserId != null && clientUserIds.Contains(a.UserId))))
                    .Select(a => a.Timestamp)
                    .ToListAsync())
                .Select(t => t.Date)
                .ToList();
            var registrations = await _db.Users.AsNoTracking()
                .Where(u => u.CreatedAt >= start && u.CreatedAt < end && (trainerUserId == null || clientUserIds.Contains(u.Id)))
                .Select(u => u.CreatedAt)
                .ToListAsync();
            var appointments = await _db.Appointments.AsNoTracking()
                .Where(a => a.StartDateTime >= start && a.StartDateTime < end && (trainerUserId == null || a.Trainer.UserId == trainerUserId))
                .GroupBy(a => a.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.Status, g => g.Count);

            var weeks = new List<EngagementWeekRow>();
            for (var monday = start.AddDays(-MondayFirst(start.DayOfWeek)); monday <= range.To; monday = monday.AddDays(7))
            {
                var from = monday < start ? start : monday;
                var to = monday.AddDays(6) > range.To ? range.To : monday.AddDays(6);
                bool InWeek(DateTime date) => date >= from && date <= to;

                var logs = activity.Count(a => InWeek(a.Date));
                weeks.Add(new EngagementWeekRow
                {
                    WeekStart = monday,
                    ActiveClients = activity.Where(a => InWeek(a.Date)).Select(a => a.ClientId).Distinct().Count(),
                    WorkoutLogs = workouts.Count(w => InWeek(w.Date)),
                    MealLogs = meals.Count(m => InWeek(m.Date)),
                    Measurements = measurements.Count(m => InWeek(m.Date)),
                    LogsPerClient = ids.Count == 0 ? 0.0m : OneDecimal((decimal)logs / ids.Count),
                    Logins = logins.Count(InWeek)
                });
            }

            var activeFrom = range.To.AddDays(1 - ActiveWindowDays);
            var statuses = Enum.GetValues<AppointmentStatus>();
            var appointmentTotal = appointments.Values.Sum();
            var months = new List<DateTime>();
            for (var month = new DateTime(start.Year, start.Month, 1); month <= range.To; month = month.AddMonths(1))
            {
                months.Add(month);
            }

            var loginDays = Enumerable.Range(0, (range.To - start).Days + 1).Select(i => start.AddDays(i)).ToList();
            return new EngagementReportViewModel
            {
                Range = range,
                AllClients = trainerUserId is null,
                ClientsInScope = ids.Count,
                ActiveClients = activity.Where(a => a.Date >= activeFrom).Select(a => a.ClientId).Distinct().Count(),
                Weeks = weeks,
                AppointmentsByStatus = statuses
                    .Select(s => new CountRow { Label = s.GetDisplayName(), Count = appointments.GetValueOrDefault(s), Percent = Percent(appointments.GetValueOrDefault(s), appointmentTotal) })
                    .ToList(),
                RegistrationsByMonth = months
                    .Select(m => new
                    {
                        Month = m,
                        Count = registrations.Count(r => r.Year == m.Year && r.Month == m.Month)
                    })
                    .Select(m => new CountRow
                    {
                        Label = m.Month.ToString("MMM yyyy", CultureInfo.CurrentCulture),
                        Count = m.Count,
                        Percent = Percent(m.Count, registrations.Count)
                    })
                    .ToList(),
                TrainerWorkload = trainerUserId is null ? (await GetTrainerWorkloadAsync(range)).Rows : [],
                Chart = new ChartSeries
                {
                    Labels = loginDays.Select(DateLabel).ToList(),
                    Datasets = [new ChartDataset { Label = "Logins", Data = loginDays.Select(d => (decimal?)logins.Count(l => l == d)).ToList() }]
                }
            };
        }

        /// <inheritdoc />
        public async Task<TrainerWorkloadReportViewModel> GetTrainerWorkloadAsync(ReportRangeViewModel range)
        {
            var start = range.From;
            var end = range.To.AddDays(1);
            var rows = await _db.TrainerProfiles.AsNoTracking()
                .Where(t => t.ApprovedAt != null)
                .OrderBy(t => t.User.FirstName)
                .ThenBy(t => t.User.LastName)
                .Select(t => new TrainerWorkloadRow
                {
                    TrainerId = t.TrainerId,
                    TrainerName = t.User.FirstName + " " + t.User.LastName,
                    ProfessionalType = t.ProfessionalType,
                    ActiveClients = t.Assignments.Count(a => a.EndDate == null),
                    MaxClients = t.MaxClients,
                    Appointments = t.Appointments.Count(a => a.StartDateTime >= start && a.StartDateTime < end),
                    AppointmentsCompleted = t.Appointments.Count(a => a.StartDateTime >= start && a.StartDateTime < end && a.Status == AppointmentStatus.Completed),
                    PlansCreated = t.WorkoutPlans.Count(p => !p.IsTemplate && p.CreatedAt >= start && p.CreatedAt < end)
                        + t.DietPlans.Count(p => p.CreatedAt >= start && p.CreatedAt < end),
                    FeedbackGiven = t.Feedback.Count(f => f.CreatedAt >= start && f.CreatedAt < end)
                })
                .ToListAsync();

            return new TrainerWorkloadReportViewModel
            {
                Range = range,
                Rows = rows,
                Chart = new ChartSeries
                {
                    Labels = rows.Select(r => r.TrainerName).ToList(),
                    Datasets =
                    [
                        new ChartDataset { Label = "Active clients", Type = "bar", Data = rows.Select(r => (decimal?)r.ActiveClients).ToList() },
                        new ChartDataset { Label = "Capacity", Dashed = true, Data = rows.Select(r => (decimal?)r.MaxClients).ToList() }
                    ]
                }
            };
        }

        /// <inheritdoc />
        public async Task<ReportFile?> ExportCsvAsync(string report, ReportRangeViewModel range, string? trainerUserId, string actingUserId)
        {
            string csv;
            string title;
            switch (report)
            {
                case ClientPerformance:
                    title = "Client performance";
                    csv = CsvExporter.ToCsv((await GetClientPerformanceAsync(range, trainerUserId)).Rows, ClientPerformanceColumns);
                    break;
                case PopularRoutines:
                    title = "Popular routines";
                    csv = CsvExporter.ToCsv((await GetPopularRoutinesAsync(range, trainerUserId)).Routines, RoutineColumns);
                    break;
                case Engagement:
                    title = "Engagement";
                    csv = CsvExporter.ToCsv((await GetEngagementAsync(range, trainerUserId)).Weeks, EngagementColumns);
                    break;
                case TrainerWorkload when trainerUserId is null:
                    title = "Trainer workload";
                    csv = CsvExporter.ToCsv((await GetTrainerWorkloadAsync(range)).Rows, WorkloadColumns);
                    break;
                default:
                    return null;
            }

            // UC40: exports are audited; the details name the report and range, never any client's figures.
            await _audit.LogAsync(actingUserId, AuditAction.Export, "Report", report, $"{title} report exported for {range.Text}.");
            var fileName = $"{report}-{_clock.Today.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}.csv";
            return new ReportFile(fileName, "text/csv", CsvExporter.ToBytes(csv));
        }

        // The clients a report covers: a trainer's actively assigned clients (BR-08), or every client whose account
        // still exists (deactivated accounts are anonymised, BR-22).
        private async Task<List<int>> ClientsInScopeAsync(string? trainerUserId) =>
            trainerUserId is null
                ? await _db.ClientProfiles.AsNoTracking()
                    .Where(c => c.User.AccountStatus != AccountStatus.Deactivated)
                    .Select(c => c.ClientId)
                    .ToListAsync()
                : await _db.TrainerAssignments.AsNoTracking()
                    .Where(a => a.Trainer.UserId == trainerUserId && a.EndDate == null)
                    .Select(a => a.ClientId)
                    .Distinct()
                    .ToListAsync();

        private static readonly CsvColumn<ClientPerformanceRow>[] ClientPerformanceColumns =
        [
            new("Client", r => r.ClientName),
            new("Trainer", r => r.TrainerName),
            new("Start weight (kg)", r => Round1(r.StartWeightKg)),
            new("Latest weight (kg)", r => Round1(r.LatestWeightKg)),
            new("Weight change (kg)", r => Round1(r.WeightChangeKg)),
            new("Start BMI", r => Round1(r.StartBmi)),
            new("Latest BMI", r => Round1(r.LatestBmi)),
            new("Workouts completed", r => r.WorkoutsCompleted),
            new("Workout adherence (%)", r => r.AdherencePercent),
            new("Average daily kcal", r => r.AverageDailyCalories),
            new("Average daily target (kcal)", r => r.AverageDailyTarget),
            new("kcal burned", r => r.CaloriesBurned),
            new("Appointments attended", r => r.AppointmentsAttended)
        ];

        private static readonly CsvColumn<RoutineUsageRow>[] RoutineColumns =
        [
            new("Template", r => r.Title),
            new("Difficulty", r => r.Difficulty),
            new("Author", r => r.AuthorName),
            new("Clients assigned", r => r.ClientsAssigned),
            new("Active plans", r => r.ActivePlans),
            new("Adherence (%)", r => r.AdherencePercent)
        ];

        private static readonly CsvColumn<EngagementWeekRow>[] EngagementColumns =
        [
            new("Week of", r => r.WeekStart),
            new("Active clients", r => r.ActiveClients),
            new("Workout logs", r => r.WorkoutLogs),
            new("Meal logs", r => r.MealLogs),
            new("Measurements", r => r.Measurements),
            new("Logs per client", r => r.LogsPerClient),
            new("Logins", r => r.Logins)
        ];

        private static readonly CsvColumn<TrainerWorkloadRow>[] WorkloadColumns =
        [
            new("Professional", r => r.TrainerName),
            new("Type", r => r.ProfessionalType),
            new("Active clients", r => r.ActiveClients),
            new("Capacity", r => r.MaxClients),
            new("Load (%)", r => r.LoadPercent),
            new("Appointments", r => r.Appointments),
            new("Appointments completed", r => r.AppointmentsCompleted),
            new("Plans created", r => r.PlansCreated),
            new("Feedback given", r => r.FeedbackGiven)
        ];

        private static decimal Percent(int count, int total) => total == 0 ? 0.0m : OneDecimal(count * 100m / total);

        private static decimal Round0(decimal value) => Math.Round(value, 0, MidpointRounding.AwayFromZero);

        private static decimal? Round1(decimal? value) => value is { } v ? OneDecimal(v) : null;

        // Exactly one decimal place (adding 0.0m sets the scale), so 80, 80.00 and 10 all export as 80.0 and 10.0.
        private static decimal OneDecimal(decimal value) => Math.Round(value, 1, MidpointRounding.AwayFromZero) + 0.0m;

        private static string DateLabel(DateTime date) => date.ToString("dd MMM yyyy", CultureInfo.CurrentCulture);

        // System.DayOfWeek starts on Sunday; the app's weeks start on Monday.
        private static int MondayFirst(DayOfWeek day) => ((int)day + 6) % 7;
    }
}
