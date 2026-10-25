using HealthTrack.Models.Enums;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// One meal of the diet plan, with whether the client has logged it today.
    /// </summary>
    public class PlannedMealViewModel
    {
        /// <summary>The planned meal's key, for logging it.</summary>
        public int DietPlanMealId { get; init; }

        /// <summary>The weekday it is for; null means every day.</summary>
        public DayOfWeek? DayOfWeek { get; init; }

        /// <summary>Breakfast, lunch, dinner or snack.</summary>
        public MealType MealType { get; init; }

        /// <summary>When the meal is planned.</summary>
        public TimeSpan ScheduledTime { get; init; }

        /// <summary>What to eat.</summary>
        public string MealDescription { get; init; } = string.Empty;

        /// <summary>Target kcal.</summary>
        public int TargetCalories { get; init; }

        /// <summary>Whether a meal was logged for it today.</summary>
        public bool IsLogged { get; init; }

        /// <summary>kcal logged for it today, if logged.</summary>
        public decimal? LoggedCalories { get; init; }
    }
}
