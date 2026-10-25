using System.ComponentModel.DataAnnotations;
using HealthTrack.Models.Enums;
using HealthTrack.Security;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// The meal log form (UC10): a planned meal from the diet plan, or an unplanned one, with its foods and servings.
    /// </summary>
    public class LogMealViewModel : IValidatableObject
    {
        /// <summary>The message when no food is given (BR-13).</summary>
        public const string NoItemsMessage = "Add at least one food you ate.";

        /// <summary>The planned meal being logged; null for an unplanned meal.</summary>
        public int? DietPlanMealId { get; set; }

        /// <summary>The day of the meal; not in the future (BR-13).</summary>
        [Required(ErrorMessage = "Enter the date.")]
        [NotInFuture]
        [DataType(DataType.Date)]
        [Display(Name = "Date")]
        public DateTime? MealDate { get; set; }

        /// <summary>Breakfast, lunch, dinner or snack.</summary>
        [Required(ErrorMessage = "Choose the meal.")]
        [Display(Name = "Meal")]
        public MealType? MealType { get; set; }

        /// <summary>The client's notes.</summary>
        [StringLength(500)]
        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        /// <summary>The foods eaten; at least one (BR-13).</summary>
        public List<LogMealItemViewModel> Items { get; set; } = [];

        /// <summary>The planned meal's description; shown only.</summary>
        [BindNever]
        [ValidateNever]
        public string? PlannedDescription { get; set; }

        /// <summary>The planned meal's target kcal; shown only.</summary>
        [BindNever]
        public int? PlannedCalories { get; set; }

        /// <summary>The active foods, grouped by category; filled by the service.</summary>
        [BindNever]
        [ValidateNever]
        public IReadOnlyList<FoodOptionViewModel> FoodOptions { get; set; } = [];

        /// <summary>
        /// A meal log needs at least one food (BR-13).
        /// </summary>
        /// <param name="validationContext">The validation context.</param>
        /// <returns>The error, if any.</returns>
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!Items.Any(i => i.FoodItemId is not null))
            {
                yield return new ValidationResult(NoItemsMessage);
            }
        }
    }
}
