using System.ComponentModel.DataAnnotations;
using HealthTrack.Models.Enums;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// The diet plan editor: the details and the existing meals are posted together; the add-meal form posts separately.
    /// </summary>
    public class DietPlanEditViewModel : IValidatableObject
    {
        /// <summary>The plan's key (from the route).</summary>
        [BindNever]
        public int DietPlanId { get; set; }

        /// <summary>Title.</summary>
        [Required(ErrorMessage = "Enter a title.")]
        [StringLength(100)]
        [Display(Name = "Title")]
        public string Title { get; set; } = string.Empty;

        /// <summary>Guidance for the client.</summary>
        [StringLength(1000)]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        /// <summary>Daily calorie target, 1,200–5,000 kcal.</summary>
        [Required(ErrorMessage = "Enter a daily calorie target.")]
        [Range(1200, 5000, ErrorMessage = "The daily calorie target must be between 1,200 and 5,000 kcal.")]
        [Display(Name = "Daily calories (kcal)")]
        public int? DailyCalorieTarget { get; set; }

        /// <summary>Daily protein target in grams.</summary>
        [Range(0, 1000, ErrorMessage = "Enter between 0 and 1,000 g.")]
        [Display(Name = "Protein (g)")]
        public int? ProteinTargetG { get; set; }

        /// <summary>Daily carbohydrate target in grams.</summary>
        [Range(0, 1000, ErrorMessage = "Enter between 0 and 1,000 g.")]
        [Display(Name = "Carbohydrate (g)")]
        public int? CarbsTargetG { get; set; }

        /// <summary>Daily fat target in grams.</summary>
        [Range(0, 1000, ErrorMessage = "Enter between 0 and 1,000 g.")]
        [Display(Name = "Fat (g)")]
        public int? FatTargetG { get; set; }

        /// <summary>First day.</summary>
        [DataType(DataType.Date)]
        [Display(Name = "Start date")]
        public DateTime? StartDate { get; set; }

        /// <summary>Last day; optional, on or after the start (BR-10).</summary>
        [DataType(DataType.Date)]
        [Display(Name = "End date")]
        public DateTime? EndDate { get; set; }

        /// <summary>The existing meals, by day and time.</summary>
        public List<PlanMealEditViewModel> Meals { get; set; } = [];

        /// <summary>Where the plan is in its lifecycle; shown only.</summary>
        [BindNever]
        public PlanStatus Status { get; set; }

        /// <summary>The client's name; shown only.</summary>
        [BindNever]
        [ValidateNever]
        public string ClientName { get; set; } = string.Empty;

        /// <summary>The target suggested from the client's latest measurement, age, activity and goal; shown only.</summary>
        [BindNever]
        public int? SuggestedCalorieTarget { get; set; }

        /// <summary>The empty add-meal form.</summary>
        [BindNever]
        [ValidateNever]
        public PlanMealViewModel NewMeal { get; set; } = new();

        /// <summary>Whether the plan can still be changed: completed plans are kept as history (BR-21).</summary>
        public bool CanEdit => Status is PlanStatus.Draft or PlanStatus.Active;

        /// <summary>The meals' target kcal on an every-day basis, for comparing with the daily target.</summary>
        public int EveryDayCalories => Meals.Where(m => m.DayOfWeek is null).Sum(m => m.TargetCalories ?? 0);

        /// <summary>
        /// The plan keeps its start date, and the end date can't be before it (BR-10).
        /// </summary>
        /// <param name="validationContext">The validation context.</param>
        /// <returns>The errors, if any.</returns>
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) =>
            PlanRules.ValidateDates(false, null, StartDate, EndDate, requireClient: false);
    }
}
