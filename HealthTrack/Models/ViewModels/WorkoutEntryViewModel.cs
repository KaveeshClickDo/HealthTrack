namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// One logged exercise in the workout history.
    /// </summary>
    public class WorkoutEntryViewModel
    {
        /// <summary>The exercise's name.</summary>
        public string ExerciseName { get; init; } = string.Empty;

        /// <summary>Sets completed.</summary>
        public int? SetsCompleted { get; init; }

        /// <summary>Reps per set.</summary>
        public int? RepsCompleted { get; init; }

        /// <summary>Load lifted in kg.</summary>
        public decimal? WeightKg { get; init; }

        /// <summary>Minutes.</summary>
        public int DurationMinutes { get; init; }

        /// <summary>Distance in km.</summary>
        public decimal? DistanceKm { get; init; }

        /// <summary>kcal burned, as stored when logged.</summary>
        public decimal CaloriesBurned { get; init; }
    }
}
