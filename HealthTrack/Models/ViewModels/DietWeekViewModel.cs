namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// The client's active diet plan (UC08): targets, today's meals with what is logged, and the week's schedule.
    /// </summary>
    public class DietWeekViewModel
    {
        /// <summary>Whether the client has an active diet plan.</summary>
        public bool HasPlan { get; init; }

        /// <summary>Title.</summary>
        public string Title { get; init; } = string.Empty;

        /// <summary>Guidance.</summary>
        public string? Description { get; init; }

        /// <summary>The trainer who wrote the plan.</summary>
        public string TrainerName { get; init; } = string.Empty;

        /// <summary>First day.</summary>
        public DateTime StartDate { get; init; }

        /// <summary>Last day, if any.</summary>
        public DateTime? EndDate { get; init; }

        /// <summary>Whether the start date has come.</summary>
        public bool HasStarted { get; init; }

        /// <summary>Today's planned meals, each marked logged or not.</summary>
        public IReadOnlyList<PlannedMealViewModel> TodaysMeals { get; init; } = [];

        /// <summary>Today's eating against the targets.</summary>
        public DailyNutritionViewModel Today { get; init; } = new();

        /// <summary>Monday to Sunday.</summary>
        public IReadOnlyList<DietDayViewModel> Days { get; init; } = [];
    }
}
