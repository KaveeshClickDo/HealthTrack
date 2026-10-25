using System.ComponentModel.DataAnnotations;
using HealthTrack.Models.Enums;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// The plan editor: the details and the existing lines are posted together; the add-exercise form posts separately.
    /// </summary>
    public class WorkoutPlanEditViewModel : IValidatableObject
    {
        /// <summary>The plan's key (from the route).</summary>
        [BindNever]
        public int WorkoutPlanId { get; set; }

        /// <summary>Whether the plan is a template; posted back so validation knows whether dates are needed.</summary>
        public bool IsTemplate { get; set; }

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

        /// <summary>The existing lines, in day and order.</summary>
        public List<PlanLineEditViewModel> Lines { get; set; } = [];

        /// <summary>Where the plan is in its lifecycle; shown only.</summary>
        [BindNever]
        public PlanStatus Status { get; set; }

        /// <summary>The client's name for a client plan; shown only.</summary>
        [BindNever]
        [ValidateNever]
        public string? ClientName { get; set; }

        /// <summary>How many client plans were copied from this template; shown only.</summary>
        [BindNever]
        public int TimesAssigned { get; set; }

        /// <summary>The empty add-exercise form.</summary>
        [BindNever]
        [ValidateNever]
        public PlanExerciseViewModel NewExercise { get; set; } = new();

        /// <summary>The active exercises, grouped by category, for the add-exercise form.</summary>
        [BindNever]
        [ValidateNever]
        public IReadOnlyList<SelectOptionViewModel> ExerciseOptions { get; set; } = [];

        /// <summary>Whether the plan can still be changed: completed and archived plans are kept as history (BR-21).</summary>
        public bool CanEdit => Status is PlanStatus.Draft or PlanStatus.Active;

        /// <summary>
        /// A client plan keeps its start date, and the end date can't be before it (BR-10).
        /// </summary>
        /// <param name="validationContext">The validation context.</param>
        /// <returns>The errors, if any.</returns>
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) =>
            PlanRules.ValidateDates(IsTemplate, null, StartDate, EndDate, requireClient: false);
    }
}
