using System.ComponentModel.DataAnnotations;
using HealthTrack.Models.Enums;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// The form for a new workout plan (UC19): a client plan (assigned clients only, with a start date) or a routine
    /// template (no client, no dates).
    /// </summary>
    public class WorkoutPlanViewModel : IValidatableObject
    {
        /// <summary>True for a reusable template, false for a client plan.</summary>
        public bool IsTemplate { get; set; }

        /// <summary>The client; required for a client plan.</summary>
        [Display(Name = "Client")]
        public int? ClientId { get; set; }

        /// <summary>Title.</summary>
        [Required(ErrorMessage = "Enter a title.")]
        [StringLength(100)]
        [Display(Name = "Title")]
        public string Title { get; set; } = string.Empty;

        /// <summary>Goal and guidance.</summary>
        [StringLength(1000)]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        /// <summary>How demanding the plan is.</summary>
        [Required(ErrorMessage = "Choose a difficulty.")]
        [Display(Name = "Difficulty")]
        public DifficultyLevel? Difficulty { get; set; }

        /// <summary>First day; required for a client plan.</summary>
        [DataType(DataType.Date)]
        [Display(Name = "Start date")]
        public DateTime? StartDate { get; set; }

        /// <summary>Last day; optional, on or after the start (BR-10).</summary>
        [DataType(DataType.Date)]
        [Display(Name = "End date")]
        public DateTime? EndDate { get; set; }

        /// <summary>The trainer's assigned clients; filled by the service, never posted.</summary>
        [BindNever]
        [ValidateNever]
        public IReadOnlyList<SelectOptionViewModel> ClientOptions { get; set; } = [];

        /// <summary>
        /// A client plan needs a client and a start date, and the end date can't be before the start (BR-10).
        /// </summary>
        /// <param name="validationContext">The validation context.</param>
        /// <returns>The errors, if any.</returns>
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) =>
            PlanRules.ValidateDates(IsTemplate, ClientId, StartDate, EndDate, requireClient: true);
    }
}
