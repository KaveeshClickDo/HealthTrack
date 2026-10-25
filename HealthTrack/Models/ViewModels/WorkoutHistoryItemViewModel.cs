namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// One logged session in the client's workout history, with its entries and any feedback on it.
    /// </summary>
    public class WorkoutHistoryItemViewModel
    {
        /// <summary>The log's key.</summary>
        public int WorkoutLogId { get; init; }

        /// <summary>The day of the session.</summary>
        public DateTime WorkoutDate { get; init; }

        /// <summary>The plan it followed; null for a free workout.</summary>
        public string? PlanTitle { get; init; }

        /// <summary>Rating of perceived exertion, 1–10.</summary>
        public int? PerceivedEffort { get; init; }

        /// <summary>The client's notes.</summary>
        public string? Notes { get; init; }

        /// <summary>The exercises done.</summary>
        public IReadOnlyList<WorkoutEntryViewModel> Entries { get; init; } = [];

        /// <summary>Trainer feedback on this session.</summary>
        public IReadOnlyList<FeedbackItemViewModel> Feedback { get; init; } = [];

        /// <summary>Total minutes of the entries.</summary>
        public int TotalMinutes => Entries.Sum(e => e.DurationMinutes);

        /// <summary>Total kcal of the entries.</summary>
        public decimal TotalCalories => Entries.Sum(e => e.CaloriesBurned);
    }
}
