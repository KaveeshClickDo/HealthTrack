namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// One food in a logged meal, with its stored nutrition snapshot.
    /// </summary>
    public class LoggedFoodViewModel
    {
        /// <summary>The food's name.</summary>
        public string FoodName { get; init; } = string.Empty;

        /// <summary>Number of servings.</summary>
        public decimal Quantity { get; init; }

        /// <summary>The size of one serving.</summary>
        public decimal ServingSize { get; init; }

        /// <summary>The serving unit.</summary>
        public string ServingUnit { get; init; } = string.Empty;

        /// <summary>kcal, as stored when logged.</summary>
        public decimal Calories { get; init; }
    }
}
