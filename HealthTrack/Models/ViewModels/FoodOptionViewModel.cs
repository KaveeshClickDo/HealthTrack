namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// One food in the meal log's picker, with what wwwroot/js/meal-log.js needs for the live kcal total.
    /// </summary>
    public class FoodOptionViewModel
    {
        /// <summary>The food's key.</summary>
        public int FoodItemId { get; init; }

        /// <summary>The food's name.</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>The food group's display name, for the option group.</summary>
        public string Category { get; init; } = string.Empty;

        /// <summary>One serving, for example "100 g".</summary>
        public string Serving { get; init; } = string.Empty;

        /// <summary>kcal per serving.</summary>
        public decimal CaloriesPerServing { get; init; }
    }
}
