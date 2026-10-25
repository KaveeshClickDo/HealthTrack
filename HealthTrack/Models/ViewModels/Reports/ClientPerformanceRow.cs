namespace HealthTrack.Models.ViewModels.Reports
{
    /// <summary>
    /// One client in the client performance report (spec 1.7).
    /// </summary>
    public class ClientPerformanceRow
    {
        /// <summary>The client profile's key.</summary>
        public int ClientId { get; init; }

        /// <summary>The client's name.</summary>
        public string ClientName { get; init; } = string.Empty;

        /// <summary>The active trainer's name, or null when unassigned.</summary>
        public string? TrainerName { get; init; }

        /// <summary>Weight at the start of the range: the last reading on or before it, else the first inside it.</summary>
        public decimal? StartWeightKg { get; init; }

        /// <summary>The latest weight up to the end of the range.</summary>
        public decimal? LatestWeightKg { get; init; }

        /// <summary>Latest minus start weight, in kg.</summary>
        public decimal? WeightChangeKg => StartWeightKg is { } start && LatestWeightKg is { } latest ? latest - start : null;

        /// <summary>BMI at the start of the range.</summary>
        public decimal? StartBmi { get; init; }

        /// <summary>The latest BMI up to the end of the range.</summary>
        public decimal? LatestBmi { get; init; }

        /// <summary>Workout sessions logged in the range.</summary>
        public int WorkoutsCompleted { get; init; }

        /// <summary>Workout adherence in the range (spec 1.10); null when nothing was scheduled.</summary>
        public int? AdherencePercent { get; init; }

        /// <summary>Average kcal eaten per day with logged meals.</summary>
        public decimal? AverageDailyCalories { get; init; }

        /// <summary>Average daily calorie target on those days, where a diet plan applied.</summary>
        public decimal? AverageDailyTarget { get; init; }

        /// <summary>kcal burned in the range's workouts.</summary>
        public decimal CaloriesBurned { get; init; }

        /// <summary>Appointments in the range that were held (Completed).</summary>
        public int AppointmentsAttended { get; init; }
    }
}
