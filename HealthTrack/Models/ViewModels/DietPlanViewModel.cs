using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// The form for a new diet plan (UC21). The trainer picks an assigned client first, so the calorie target can be
    /// pre-filled from that client's TDEE and goal (spec 1.10).
    /// </summary>
    public class DietPlanViewModel : IValidatableObject
    {
        /// <summary>The client; one of the trainer's assigned clients (BR-08).</summary>
        [Required(ErrorMessage = "Choose a client.")]
        [Display(Name = "Client")]
        public int? ClientId { get; set; }

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

        /// <summary>The chosen client's name; shown only.</summary>
        [BindNever]
        [ValidateNever]
        public string? ClientName { get; set; }

        /// <summary>The target suggested from the client's latest measurement, age, activity and goal; shown only.</summary>
        [BindNever]
        public int? SuggestedCalorieTarget { get; set; }

        /// <summary>The trainer's assigned clients, for choosing one; filled by the service.</summary>
        [BindNever]
        [ValidateNever]
        public IReadOnlyList<SelectOptionViewModel> ClientOptions { get; set; } = [];

        /// <summary>
        /// A plan needs a start date, and the end date can't be before it (BR-10).
        /// </summary>
        /// <param name="validationContext">The validation context.</param>
        /// <returns>The errors, if any.</returns>
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) =>
            PlanRules.ValidateDates(false, ClientId, StartDate, EndDate, requireClient: false);
    }
}
