namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// One recent session in the trainer's view of a client.
    /// </summary>
    public class RecentWorkoutViewModel
    {
        /// <summary>The log's key, for giving feedback on it.</summary>
        public int WorkoutLogId { get; init; }

        /// <summary>The day of the session.</summary>
        public DateTime WorkoutDate { get; init; }

        /// <summary>The plan followed, or null for a free workout.</summary>
        public string? PlanTitle { get; init; }

        /// <summary>Total minutes.</summary>
        public int Minutes { get; init; }

        /// <summary>Total kcal burned.</summary>
        public decimal Calories { get; init; }

        /// <summary>Number of exercises.</summary>
        public int Exercises { get; init; }
    }
}
