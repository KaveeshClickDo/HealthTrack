using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HealthTrack.Models.Enums;

namespace HealthTrack.Models.Entities
{
    /// <summary>
    /// A meal a client ate. Meal totals are computed from the items, not stored.
    /// </summary>
    public class MealLog
    {
        /// <summary>Primary key.</summary>
        public int MealLogId { get; set; }

        /// <summary>The client who ate the meal (R23).</summary>
        public int ClientId { get; set; }

        /// <summary>The planned meal this log follows; null for an unplanned meal (R24).</summary>
        public int? DietPlanMealId { get; set; }

        /// <summary>The day of the meal; not in the future (BR-13).</summary>
        [Column(TypeName = "date")]
        public DateTime MealDate { get; set; }

        /// <summary>Breakfast, lunch, dinner or snack.</summary>
        public MealType MealType { get; set; }

        /// <summary>The client's notes.</summary>
        [MaxLength(500)]
        public string? Notes { get; set; }

        /// <summary>When the log was saved.</summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>The client who ate the meal (R23).</summary>
        public virtual ClientProfile Client { get; set; } = null!;

        /// <summary>The planned meal this log follows (R24).</summary>
        public virtual DietPlanMeal? DietPlanMeal { get; set; }

        /// <summary>The foods eaten; a saved log has at least one (R25, BR-13).</summary>
        public virtual ICollection<MealLogItem> Items { get; set; } = new List<MealLogItem>();

        /// <summary>Total kcal of the loaded items.</summary>
        [NotMapped]
        public decimal TotalCalories => Items.Sum(i => i.Calories);

        /// <summary>Total protein in grams of the loaded items.</summary>
        [NotMapped]
        public decimal TotalProteinG => Items.Sum(i => i.ProteinG);

        /// <summary>Total carbohydrate in grams of the loaded items.</summary>
        [NotMapped]
        public decimal TotalCarbsG => Items.Sum(i => i.CarbsG);

        /// <summary>Total fat in grams of the loaded items.</summary>
        [NotMapped]
        public decimal TotalFatG => Items.Sum(i => i.FatG);

        /// <summary>
        /// Adds a food to the meal and stores its nutrition as a snapshot: quantity × the per-serving values (spec 1.10, BR-14),
        /// so later changes to the food library don't rewrite history.
        /// </summary>
        /// <param name="food">The food eaten.</param>
        /// <param name="quantity">Number of servings, 0.25–20.</param>
        /// <returns>The new item, already added to <see cref="Items"/>.</returns>
        public MealLogItem AddItem(FoodItem food, decimal quantity)
        {
            ArgumentNullException.ThrowIfNull(food);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

            // Only the foreign key is set: attaching a library entity loaded without tracking would make EF Core insert it again.
            var item = new MealLogItem
            {
                FoodItemId = food.FoodItemId,
                Quantity = quantity,
                Calories = food.CaloriesFor(quantity),
                ProteinG = Snapshot(quantity, food.ProteinG),
                CarbsG = Snapshot(quantity, food.CarbsG),
                FatG = Snapshot(quantity, food.FatG)
            };

            Items.Add(item);
            return item;
        }

        // Rounded to the 2 decimal places the snapshot columns store.
        private static decimal Snapshot(decimal quantity, decimal perServing) =>
            Math.Round(quantity * perServing, 2, MidpointRounding.AwayFromZero);
    }
}
