using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// One food row of the meal log form; nutrition is worked out on the server (BR-14).
    /// </summary>
    public class LogMealItemViewModel
    {
        /// <summary>The food eaten; active foods only (BR-21).</summary>
        [Required(ErrorMessage = "Choose a food.")]
        [Display(Name = "Food")]
        public int? FoodItemId { get; set; }

        /// <summary>Number of servings, 0.25–20.</summary>
        [Required(ErrorMessage = "Enter the servings.")]
        [Range(0.25, 20.0, ErrorMessage = "Servings must be between 0.25 and 20.")]
        [Display(Name = "Servings")]
        public decimal? Quantity { get; set; } = 1m;
    }
}
