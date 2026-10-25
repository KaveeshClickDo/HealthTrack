using System.Globalization;
using HealthTrack.Data;
using HealthTrack.Helpers;
using HealthTrack.Models.Entities;
using HealthTrack.Models.Enums;
using HealthTrack.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Services
{
    /// <summary>
    /// Measurements, progress trends, adherence and the suggested calorie target.
    /// </summary>
    public class ProgressService : IProgressService
    {
        // How many recent rows the trainer's progress page lists in each table.
        private const int RecentCount = 5;

        private readonly ApplicationDbContext _db;
        private readonly IAssignmentService _assignments;
        private readonly IAuditService _audit;
        private readonly IClock _clock;

        /// <summary>
        /// Creates the service.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="assignments">Checks that a trainer may reach a client (BR-08).</param>
        /// <param name="audit">Records new measurements.</param>
        /// <param name="clock">Supplies the time.</param>
        public ProgressService(ApplicationDbContext db, IAssignmentService assignments, IAuditService audit, IClock clock)
        {
            _db = db;
            _assignments = assignments;
            _audit = audit;
            _clock = clock;
        }

        /// <inheritdoc />
        public async Task<MeasurementViewModel> GetMeasurementFormAsync(int clientId)
        {
            var lastHeight = await _db.HealthMeasurements.AsNoTracking()
                .Where(m => m.ClientId == clientId)
                .OrderByDescending(m => m.MeasuredAt)
                .Select(m => (decimal?)m.HeightCm)
                .FirstOrDefaultAsync();

            // Minutes only: the datetime-local input has no seconds.
            var now = _clock.Now;
            return new MeasurementViewModel
            {
                MeasuredAt = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0),
                HeightCm = lastHeight
            };
        }

        /// <inheritdoc />
        public async Task<int?> AddMeasurementAsync(string clientUserId, MeasurementViewModel model)
        {
            var clientId = await _db.ClientProfiles.Where(c => c.UserId == clientUserId).Select(c => (int?)c.ClientId).FirstOrDefaultAsync();
            if (clientId is null)
            {
                return null;
            }

            // BR-13: measurements can't be future-dated.
            if (model.MeasuredAt!.Value > _clock.Now)
            {
                throw new BusinessRuleException("BR-13", "A measurement can't be in the future.");
            }

            // BMI isn't stored or posted: HealthMeasurement.Bmi works it out from weight and height (BR-14).
            var measurement = new HealthMeasurement
            {
                ClientId = clientId.Value,
                MeasuredAt = model.MeasuredAt.Value,
                WeightKg = model.WeightKg!.Value,
                HeightCm = model.HeightCm!.Value,
                BodyFatPercent = model.BodyFatPercent,
                WaistCm = model.WaistCm,
                RestingHeartRate = model.RestingHeartRate,
                SystolicBp = model.SystolicBp,
                DiastolicBp = model.DiastolicBp,
                BloodGlucose = model.BloodGlucose,
                Notes = model.Notes.NullIfBlank()
            };

            _db.HealthMeasurements.Add(measurement);
            await _db.SaveChangesAsync();

            // The audit row says what happened without health values.
            await _audit.LogAsync(clientUserId, AuditAction.Create, nameof(HealthMeasurement), Key(measurement.MeasurementId),
                "Measurement recorded.");
            return measurement.MeasurementId;
        }

        /// <inheritdoc />
        public async Task<MeasurementRowViewModel?> GetLatestMeasurementAsync(int clientId)
        {
            var latest = await _db.HealthMeasurements.AsNoTracking()
                .Where(m => m.ClientId == clientId)
                .OrderByDescending(m => m.MeasuredAt)
                .FirstOrDefaultAsync();
            return latest is null ? null : Row(latest, includeNotes: true);
        }

        /// <inheritdoc />
        public async Task<MeasurementsPageViewModel> GetMeasurementsAsync(int clientId, int page)
        {
            var measurements = _db.HealthMeasurements.AsNoTracking().Where(m => m.ClientId == clientId);
            var paged = await measurements.OrderByDescending(m => m.MeasuredAt).ToPagedResultAsync(page);
            var first = await measurements.OrderBy(m => m.MeasuredAt).FirstOrDefaultAsync();
            var latest = await measurements.OrderByDescending(m => m.MeasuredAt).FirstOrDefaultAsync();

            return new MeasurementsPageViewModel
            {
                Latest = latest is null ? null : Row(latest, includeNotes: true),
                First = first is null ? null : Row(first, includeNotes: true),
                TargetWeightKg = await TargetWeightAsync(clientId),
                Results = new PagedResult<MeasurementRowViewModel>(
                    paged.Items.Select(m => Row(m, includeNotes: true)).ToList(), paged.Page, paged.PageSize, paged.TotalCount)
            };
        }

        /// <inheritdoc />
        public async Task<ChartSeries> GetWeightTrendAsync(int clientId, DateTime from, DateTime to)
        {
            var days = await DailyReadingsAsync(clientId, from, to);
            var datasets = new List<ChartDataset>
            {
                new() { Label = "Weight (kg)", Data = days.Select(m => (decimal?)m.WeightKg).ToList() }
            };

            // The target is a flat reference line across the same days.
            if (await TargetWeightAsync(clientId) is { } target && days.Count > 0)
            {
                datasets.Add(new ChartDataset { Label = "Target weight (kg)", Data = days.Select(_ => (decimal?)target).ToList(), Dashed = true });
            }

            return new ChartSeries { Labels = days.Select(m => DateLabel(m.MeasuredAt)).ToList(), Datasets = datasets };
        }

        /// <inheritdoc />
        public async Task<ChartSeries> GetBmiTrendAsync(int clientId, DateTime from, DateTime to)
        {
            var days = await DailyReadingsAsync(clientId, from, to);
            return new ChartSeries
            {
                Labels = days.Select(m => DateLabel(m.MeasuredAt)).ToList(),
                Datasets = [new ChartDataset { Label = "BMI", Data = days.Select(m => (decimal?)m.Bmi).ToList() }]
            };
        }

        /// <inheritdoc />
        public async Task<ChartSeries> GetCaloriesTrendAsync(int clientId, DateTime from, DateTime to)
        {
            var first = from.Date;
            var last = to.Date;

            var burned = await _db.WorkoutLogs.AsNoTracking()
                .Where(l => l.ClientId == clientId && l.WorkoutDate >= first && l.WorkoutDate <= last)
                .SelectMany(l => l.Entries.Select(e => new { Date = l.WorkoutDate, e.CaloriesBurned }))
                .ToListAsync();
            var eaten = await _db.MealLogItems.AsNoTracking()
                .Where(i => i.MealLog.ClientId == clientId && i.MealLog.MealDate >= first && i.MealLog.MealDate <= last)
                .Select(i => new { Date = i.MealLog.MealDate, i.Calories })
                .ToListAsync();
            var plans = await NutritionDays.LoadFollowedPlansAsync(_db, clientId);

            var labels = new List<string>();
            var burnedData = new List<decimal?>();
            var eatenData = new List<decimal?>();
            var targetData = new List<decimal?>();

            // Monday-start weeks; the first and last weeks only count the days inside the range.
            for (var weekStart = first.AddDays(-MondayFirst(first.DayOfWeek)); weekStart <= last; weekStart = weekStart.AddDays(7))
            {
                var start = weekStart < first ? first : weekStart;
                var end = weekStart.AddDays(6) > last ? last : weekStart.AddDays(6);
                var weekBurned = burned.Where(b => b.Date >= start && b.Date <= end).ToList();
                var weekEaten = eaten.Where(e => e.Date >= start && e.Date <= end).ToList();

                // The target is added up over the days a diet plan applied, so it matches the eating it is compared with.
                var targets = Enumerable.Range(0, (end - start).Days + 1)
                    .Select(offset => NutritionDays.PlanOn(plans, start.AddDays(offset))?.DailyCalorieTarget)
                    .Where(t => t is not null)
                    .ToList();

                labels.Add("Week of " + DateLabel(weekStart));
                burnedData.Add(weekBurned.Count == 0 ? null : Math.Round(weekBurned.Sum(b => b.CaloriesBurned), 0, MidpointRounding.AwayFromZero));
                eatenData.Add(weekEaten.Count == 0 ? null : Math.Round(weekEaten.Sum(e => e.Calories), 0, MidpointRounding.AwayFromZero));
                targetData.Add(targets.Count == 0 ? null : targets.Sum(t => t!.Value));
            }

            return new ChartSeries
            {
                Labels = labels,
                Datasets =
                [
                    new ChartDataset { Label = "kcal burned", Data = burnedData, Type = "bar" },
                    new ChartDataset { Label = "kcal eaten", Data = eatenData, Type = "bar" },
                    new ChartDataset { Label = "Diet target (kcal)", Data = targetData, Dashed = true }
                ]
            };
        }

        /// <inheritdoc />
        public async Task<ChartSeries> GetVitalsTrendAsync(int clientId, DateTime from, DateTime to)
        {
            var days = (await DailyReadingsAsync(clientId, from, to))
                .Where(m => m.RestingHeartRate is not null || m.SystolicBp is not null || m.DiastolicBp is not null)
                .ToList();
            return new ChartSeries
            {
                Labels = days.Select(m => DateLabel(m.MeasuredAt)).ToList(),
                Datasets =
                [
                    new ChartDataset { Label = "Resting heart rate (bpm)", Data = days.Select(m => (decimal?)m.RestingHeartRate).ToList() },
                    new ChartDataset { Label = "Systolic (mmHg)", Data = days.Select(m => (decimal?)m.SystolicBp).ToList() },
                    new ChartDataset { Label = "Diastolic (mmHg)", Data = days.Select(m => (decimal?)m.DiastolicBp).ToList() }
                ]
            };
        }

        /// <inheritdoc />
        public async Task<AdherenceViewModel> GetAdherenceAsync(int clientId, DateTime from, DateTime to) =>
            // Spec 1.10; days still to come aren't missed yet, and a newer plan takes over from its start date (BR-09).
            (await WorkoutAdherence.ForClientsAsync(_db, [clientId], from, to, _clock.Today))[clientId];

        /// <inheritdoc />
        public async Task<int?> SuggestCalorieTargetAsync(int clientId)
        {
            var client = await _db.ClientProfiles.AsNoTracking()
                .Where(c => c.ClientId == clientId)
                .Select(c => new { c.FitnessGoal, c.ActivityLevel, c.User.DateOfBirth, c.User.Gender })
                .FirstOrDefaultAsync();
            var latest = await _db.HealthMeasurements.AsNoTracking()
                .Where(m => m.ClientId == clientId)
                .OrderByDescending(m => m.MeasuredAt)
                .Select(m => new { m.WeightKg, m.HeightCm })
                .FirstOrDefaultAsync();
            if (client is null || latest is null)
            {
                return null;
            }

            // Spec 1.10: Mifflin–St Jeor BMR × activity factor, then the goal's adjustment and the 1,200 kcal floor.
            var age = HealthCalculator.CalculateAge(client.DateOfBirth, _clock.Today);
            var bmr = HealthCalculator.CalculateBmr(latest.WeightKg, latest.HeightCm, age, client.Gender);
            var tdee = HealthCalculator.CalculateTdee(bmr, client.ActivityLevel);
            return HealthCalculator.SuggestCalorieTarget(tdee, client.FitnessGoal);
        }

        /// <inheritdoc />
        public async Task<ClientProgressViewModel> GetClientProgressAsync(int clientId, ProgressRangeViewModel range)
        {
            var measurements = _db.HealthMeasurements.AsNoTracking().Where(m => m.ClientId == clientId);
            var first = await measurements.OrderBy(m => m.MeasuredAt).FirstOrDefaultAsync();
            var latest = await measurements.OrderByDescending(m => m.MeasuredAt).FirstOrDefaultAsync();

            return new ClientProgressViewModel
            {
                Range = range,
                First = first is null ? null : Row(first, includeNotes: false),
                Latest = latest is null ? null : Row(latest, includeNotes: false),
                TargetWeightKg = await TargetWeightAsync(clientId)
            };
        }

        /// <inheritdoc />
        public async Task<TrainerClientProgressViewModel?> GetTrainerClientProgressAsync(string trainerUserId, int clientId,
            ProgressRangeViewModel range)
        {
            var trainerId = await _db.TrainerProfiles.Where(t => t.UserId == trainerUserId).Select(t => (int?)t.TrainerId).FirstOrDefaultAsync();

            // BR-08: only a client actively assigned to this trainer.
            if (trainerId is null || !await _assignments.IsTrainerAssignedToClientAsync(trainerId.Value, clientId))
            {
                return null;
            }

            var client = await _db.ClientProfiles.AsNoTracking()
                .Include(c => c.User)
                .FirstAsync(c => c.ClientId == clientId);
            var shares = client.ShareMedicalInfoWithTrainer;

            var recentMeasurements = (await _db.HealthMeasurements.AsNoTracking()
                    .Where(m => m.ClientId == clientId)
                    .OrderByDescending(m => m.MeasuredAt)
                    .Take(RecentCount)
                    .ToListAsync())
                .Select(m => Row(m, includeNotes: shares))
                .ToList();

            var recentWorkouts = await _db.WorkoutLogs.AsNoTracking()
                .Where(l => l.ClientId == clientId)
                .OrderByDescending(l => l.WorkoutDate)
                .ThenByDescending(l => l.WorkoutLogId)
                .Take(RecentCount)
                .Select(l => new RecentWorkoutViewModel
                {
                    WorkoutLogId = l.WorkoutLogId,
                    WorkoutDate = l.WorkoutDate,
                    PlanTitle = l.WorkoutPlan != null ? l.WorkoutPlan.Title : null,
                    Minutes = l.Entries.Sum(e => e.DurationMinutes),
                    Calories = l.Entries.Sum(e => e.CaloriesBurned),
                    Exercises = l.Entries.Count
                })
                .ToListAsync();

            var recentMealDates = await _db.MealLogs.AsNoTracking()
                .Where(m => m.ClientId == clientId)
                .Select(m => m.MealDate)
                .Distinct()
                .OrderByDescending(d => d)
                .Take(RecentCount)
                .ToListAsync();

            return new TrainerClientProgressViewModel
            {
                ClientId = clientId,
                ClientName = client.User.FullName,
                FitnessGoal = client.FitnessGoal,
                ActivityLevel = client.ActivityLevel,
                Age = HealthCalculator.CalculateAge(client.User.DateOfBirth, _clock.Today),
                TargetWeightKg = client.TargetWeightKg,
                // The client decides whether the trainer sees the encrypted medical fields.
                SharesMedicalInfo = shares,
                MedicalConditions = shares ? client.MedicalConditions : null,
                Allergies = shares ? client.Allergies : null,
                CurrentMedications = shares ? client.CurrentMedications : null,
                Range = range,
                Latest = recentMeasurements.FirstOrDefault(),
                RecentMeasurements = recentMeasurements,
                Adherence = await GetAdherenceAsync(clientId, range.From, range.To),
                Charts = new ProgressChartsViewModel
                {
                    Weight = await GetWeightTrendAsync(clientId, range.From, range.To),
                    Bmi = await GetBmiTrendAsync(clientId, range.From, range.To),
                    Calories = await GetCaloriesTrendAsync(clientId, range.From, range.To),
                    Vitals = await GetVitalsTrendAsync(clientId, range.From, range.To)
                },
                RecentWorkouts = recentWorkouts,
                RecentMealDays = await NutritionDays.GetDaysAsync(_db, clientId, recentMealDates)
            };
        }

        // The last reading of each day in the range, oldest first, so a chart has one point a day.
        private async Task<List<HealthMeasurement>> DailyReadingsAsync(int clientId, DateTime from, DateTime to)
        {
            var start = from.Date;
            var end = to.Date.AddDays(1);
            var readings = await _db.HealthMeasurements.AsNoTracking()
                .Where(m => m.ClientId == clientId && m.MeasuredAt >= start && m.MeasuredAt < end)
                .OrderBy(m => m.MeasuredAt)
                .ToListAsync();
            return readings.GroupBy(m => m.MeasuredAt.Date).Select(g => g.Last()).ToList();
        }

        private Task<decimal?> TargetWeightAsync(int clientId) =>
            _db.ClientProfiles.Where(c => c.ClientId == clientId).Select(c => c.TargetWeightKg).FirstOrDefaultAsync();

        private static MeasurementRowViewModel Row(HealthMeasurement m, bool includeNotes) => new()
        {
            MeasurementId = m.MeasurementId,
            MeasuredAt = m.MeasuredAt,
            WeightKg = m.WeightKg,
            HeightCm = m.HeightCm,
            Bmi = m.Bmi,
            BmiCategory = m.BmiCategory,
            BodyFatPercent = m.BodyFatPercent,
            WaistCm = m.WaistCm,
            RestingHeartRate = m.RestingHeartRate,
            SystolicBp = m.SystolicBp,
            DiastolicBp = m.DiastolicBp,
            BloodGlucose = m.BloodGlucose,
            Notes = includeNotes ? m.Notes : null
        };

        private static string DateLabel(DateTime date) => date.ToString("dd MMM yyyy", CultureInfo.CurrentCulture);

        // System.DayOfWeek starts on Sunday; the app's weeks start on Monday.
        private static int MondayFirst(DayOfWeek day) => ((int)day + 6) % 7;

        private static string Key(int id) => id.ToString(CultureInfo.InvariantCulture);
    }
}
