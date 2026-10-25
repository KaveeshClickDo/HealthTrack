using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Models.Entities
{
    /// <summary>
    /// One food in a logged meal (resolves MealLog many-to-many FoodItem). Created by <see cref="MealLog.AddItem"/>.
    /// </summary>
    public class MealLogItem
    {
        /// <summary>Primary key.</summary>
        public int MealLogItemId { get; set; }

        /// <summary>The meal (R25).</summary>
        public int MealLogId { get; set; }

        /// <summary>The food eaten (R26).</summary>
        public int FoodItemId { get; set; }

        /// <summary>Number of servings, 0.25–20.</summary>
        [Precision(6, 2)]
        public decimal Quantity { get; set; }

        /// <summary>Snapshot: Quantity × CaloriesPerServing.</summary>
        [Precision(7, 2)]
        public decimal Calories { get; set; }

        /// <summary>Snapshot: Quantity × protein per serving.</summary>
        [Precision(6, 2)]
        public decimal ProteinG { get; set; }

        /// <summary>Snapshot: Quantity × carbohydrate per serving.</summary>
        [Precision(6, 2)]
        public decimal CarbsG { get; set; }

        /// <summary>Snapshot: Quantity × fat per serving.</summary>
        [Precision(6, 2)]
        public decimal FatG { get; set; }

        /// <summary>The meal (R25).</summary>
        public virtual MealLog MealLog { get; set; } = null!;

        /// <summary>The food eaten (R26).</summary>
        public virtual FoodItem FoodItem { get; set; } = null!;
    }
}
