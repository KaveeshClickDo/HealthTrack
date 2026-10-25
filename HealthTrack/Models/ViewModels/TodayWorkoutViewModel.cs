namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// Today's workout on the client's dashboard: what the active plan prescribes and whether it is done.
    /// </summary>
    public class TodayWorkoutViewModel
    {
        /// <summary>Whether the client has an active plan that applies today.</summary>
        public bool HasPlan { get; init; }

        /// <summary>The plan's title.</summary>
        public string? PlanTitle { get; init; }

        /// <summary>Today's exercises with their prescription, for example "Push-up – 3 × 8"; empty on a rest day.</summary>
        public IReadOnlyList<string> Exercises { get; init; } = [];

        /// <summary>Whether today is a rest day of the plan.</summary>
        public bool IsRestDay => HasPlan && Exercises.Count == 0;

        /// <summary>Whether a workout is already logged today.</summary>
        public bool LoggedToday { get; init; }
    }
}
