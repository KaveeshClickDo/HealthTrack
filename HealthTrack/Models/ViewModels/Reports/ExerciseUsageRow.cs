using HealthTrack.Models.Enums;

namespace HealthTrack.Models.ViewModels.Reports
{
    /// <summary>
    /// One exercise in the popular routines report's top 10 (spec 1.7).
    /// </summary>
    public class ExerciseUsageRow
    {
        /// <summary>The exercise's name.</summary>
        public string ExerciseName { get; init; } = string.Empty;

        /// <summary>The exercise's category.</summary>
        public ExerciseCategory Category { get; init; }

        /// <summary>How often it was logged in the range.</summary>
        public int TimesLogged { get; init; }

        /// <summary>Minutes logged in all.</summary>
        public int TotalMinutes { get; init; }

        /// <summary>kcal burned in all.</summary>
        public decimal CaloriesBurned { get; init; }
    }
}
