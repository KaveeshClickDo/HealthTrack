using System.ComponentModel.DataAnnotations;
using HealthTrack.Models.Enums;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// One existing meal in the diet plan editor. The day and meal type are fixed once added; the rest can change.
    /// </summary>
    public class PlanMealEditViewModel
    {
        /// <summary>The meal's key.</summary>
        public int DietPlanMealId { get; set; }

        /// <summary>When the meal is planned.</summary>
        [Required(ErrorMessage = "Enter a time.")]
        [DataType(DataType.Time)]
        [Display(Name = "Time")]
        public TimeSpan? ScheduledTime { get; set; }

        /// <summary>What to eat.</summary>
        [Required(ErrorMessage = "Describe the meal.")]
        [StringLength(500)]
        [Display(Name = "What to eat")]
        public string MealDescription { get; set; } = string.Empty;

        /// <summary>Target kcal, 50–2,000.</summary>
        [Required(ErrorMessage = "Enter the target calories.")]
        [Range(50, 2000, ErrorMessage = "A meal's target must be between 50 and 2,000 kcal.")]
        [Display(Name = "Target (kcal)")]
        public int? TargetCalories { get; set; }

        /// <summary>Tick to remove the meal when the plan is saved; refused once the meal has been logged (BR-21).</summary>
        [Display(Name = "Remove")]
        public bool Remove { get; set; }

        /// <summary>The weekday, or null for every day; shown only.</summary>
        [BindNever]
        public DayOfWeek? DayOfWeek { get; set; }

        /// <summary>The meal type; shown only.</summary>
        [BindNever]
        public MealType MealType { get; set; }

        /// <summary>How many times the client logged this meal; shown only.</summary>
        [BindNever]
        public int TimesLogged { get; set; }
    }
}
