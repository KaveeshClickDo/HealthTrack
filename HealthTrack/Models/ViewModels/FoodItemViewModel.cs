using System.ComponentModel.DataAnnotations;
using HealthTrack.Models.Enums;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// The form for adding or editing a food library entry (UC32). No ID means a new food.
    /// </summary>
    public class FoodItemViewModel
    {
        /// <summary>The food being edited; null for a new one.</summary>
        public int? FoodItemId { get; set; }

        /// <summary>Unique name.</summary>
        [Required(ErrorMessage = "Enter a name.")]
        [StringLength(100)]
        [Display(Name = "Name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>The food group.</summary>
        [Required(ErrorMessage = "Choose a category.")]
        [Display(Name = "Category")]
        public FoodCategory? Category { get; set; }

        /// <summary>The size of one serving.</summary>
        [Required(ErrorMessage = "Enter the serving size.")]
        [Range(0.1, 5000.0, ErrorMessage = "The serving size must be between 0.1 and 5,000.")]
        [Display(Name = "Serving size")]
        public decimal? ServingSize { get; set; }

        /// <summary>The serving unit: g, ml, piece, cup, slice or tbsp.</summary>
        [Required(ErrorMessage = "Choose a unit.")]
        [RegularExpression("^(g|ml|piece|cup|slice|tbsp)$", ErrorMessage = "Choose g, ml, piece, cup, slice or tbsp.")]
        [Display(Name = "Unit")]
        public string ServingUnit { get; set; } = "g";

        /// <summary>kcal per serving, 0–2,000.</summary>
        [Required(ErrorMessage = "Enter the calories.")]
        [Range(0.0, 2000.0, ErrorMessage = "Calories must be between 0 and 2,000 per serving.")]
        [Display(Name = "kcal per serving")]
        public decimal? CaloriesPerServing { get; set; }

        /// <summary>Protein in grams per serving.</summary>
        [Required(ErrorMessage = "Enter the protein.")]
        [Range(0.0, 500.0, ErrorMessage = "Protein must be between 0 and 500 g.")]
        [Display(Name = "Protein (g)")]
        public decimal? ProteinG { get; set; }

        /// <summary>Carbohydrate in grams per serving.</summary>
        [Required(ErrorMessage = "Enter the carbohydrate.")]
        [Range(0.0, 500.0, ErrorMessage = "Carbohydrate must be between 0 and 500 g.")]
        [Display(Name = "Carbs (g)")]
        public decimal? CarbsG { get; set; }

        /// <summary>Fat in grams per serving.</summary>
        [Required(ErrorMessage = "Enter the fat.")]
        [Range(0.0, 500.0, ErrorMessage = "Fat must be between 0 and 500 g.")]
        [Display(Name = "Fat (g)")]
        public decimal? FatG { get; set; }

        /// <summary>Whether the food is active; shown on the form, changed with the activate/deactivate button.</summary>
        public bool IsActive { get; set; } = true;
    }
}
