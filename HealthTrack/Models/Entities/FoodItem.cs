using System.ComponentModel.DataAnnotations;
using HealthTrack.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Models.Entities
{
    /// <summary>
    /// A food library entry with nutrition per serving. Inactive foods are hidden, never deleted (BR-21).
    /// </summary>
    public class FoodItem
    {
        /// <summary>The serving units the library uses (spec 2.6).</summary>
        public static readonly IReadOnlyList<string> ServingUnits = ["g", "ml", "piece", "cup", "slice", "tbsp"];

        /// <summary>Primary key.</summary>
        public int FoodItemId { get; set; }

        /// <summary>Unique name.</summary>
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        /// <summary>The food group.</summary>
        public FoodCategory Category { get; set; }

        /// <summary>The size of one serving in <see cref="ServingUnit"/>, for example 100.</summary>
        [Precision(7, 2)]
        public decimal ServingSize { get; set; }

        /// <summary>The unit of the serving: g, ml, piece, cup, slice or tbsp.</summary>
        [MaxLength(20)]
        public string ServingUnit { get; set; } = string.Empty;

        /// <summary>kcal per serving, 0–2,000.</summary>
        [Precision(7, 2)]
        public decimal CaloriesPerServing { get; set; }

        /// <summary>Protein in grams per serving.</summary>
        [Precision(6, 2)]
        public decimal ProteinG { get; set; }

        /// <summary>Carbohydrate in grams per serving.</summary>
        [Precision(6, 2)]
        public decimal CarbsG { get; set; }

        /// <summary>Fat in grams per serving.</summary>
        [Precision(6, 2)]
        public decimal FatG { get; set; }

        /// <summary>Whether the food can be picked for new meal logs. Default true.</summary>
        public bool IsActive { get; set; } = true;

        /// <summary>Logged portions of this food (R26).</summary>
        public virtual ICollection<MealLogItem> MealLogItems { get; set; } = new List<MealLogItem>();

        /// <summary>
        /// kcal in a number of servings: quantity × CaloriesPerServing (spec 1.10, BR-14).
        /// </summary>
        /// <param name="quantity">Number of servings, 0.25–20.</param>
        /// <returns>kcal to 2 decimal places.</returns>
        public decimal CaloriesFor(decimal quantity) => Math.Round(quantity * CaloriesPerServing, 2, MidpointRounding.AwayFromZero);
    }
}
