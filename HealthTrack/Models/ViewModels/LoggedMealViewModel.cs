using HealthTrack.Models.Enums;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// One logged meal with its foods.
    /// </summary>
    public class LoggedMealViewModel
    {
        /// <summary>The log's key.</summary>
        public int MealLogId { get; init; }

        /// <summary>Breakfast, lunch, dinner or snack.</summary>
        public MealType MealType { get; init; }

        /// <summary>The planned meal it followed, if any.</summary>
        public string? PlannedDescription { get; init; }

        /// <summary>The client's notes.</summary>
        public string? Notes { get; init; }

        /// <summary>The foods eaten.</summary>
        public IReadOnlyList<LoggedFoodViewModel> Items { get; init; } = [];

        /// <summary>Total kcal of the foods.</summary>
        public decimal TotalCalories => Items.Sum(i => i.Calories);
    }
}
