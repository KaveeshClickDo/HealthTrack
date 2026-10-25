namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// The meals a diet plan schedules on one weekday: the every-day meals plus that day's own, in time order.
    /// </summary>
    public class DietDayViewModel
    {
        /// <summary>The weekday.</summary>
        public DayOfWeek DayOfWeek { get; init; }

        /// <summary>The day's meals by time.</summary>
        public IReadOnlyList<PlannedMealViewModel> Meals { get; init; } = [];

        /// <summary>Whether this is today.</summary>
        public bool IsToday { get; init; }

        /// <summary>The meals' target kcal added up.</summary>
        public int PlannedCalories => Meals.Sum(m => m.TargetCalories);
    }
}
