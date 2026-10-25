using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// One existing line in the plan editor: its prescription can be changed, or it can be removed.
    /// </summary>
    public class PlanLineEditViewModel : IValidatableObject
    {
        /// <summary>The line's key.</summary>
        public int WorkoutPlanExerciseId { get; set; }

        /// <summary>Sets, 1–20.</summary>
        [Range(1, 20, ErrorMessage = "Sets must be between 1 and 20.")]
        [Display(Name = "Sets")]
        public int? Sets { get; set; }

        /// <summary>Reps per set, 1–100.</summary>
        [Range(1, 100, ErrorMessage = "Reps must be between 1 and 100.")]
        [Display(Name = "Reps")]
        public int? Reps { get; set; }

        /// <summary>Duration in minutes, 1–300.</summary>
        [Range(1, 300, ErrorMessage = "The duration must be between 1 and 300 minutes.")]
        [Display(Name = "Minutes")]
        public int? DurationMinutes { get; set; }

        /// <summary>Rest between sets in seconds, 0–600.</summary>
        [Range(0, 600, ErrorMessage = "Rest must be between 0 and 600 seconds.")]
        [Display(Name = "Rest (s)")]
        public int? RestSeconds { get; set; }

        /// <summary>A coaching cue.</summary>
        [StringLength(250)]
        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        /// <summary>Remove the line from the plan.</summary>
        [Display(Name = "Remove")]
        public bool Remove { get; set; }

        /// <summary>The exercise's name; shown only.</summary>
        [BindNever]
        [ValidateNever]
        public string ExerciseName { get; set; } = string.Empty;

        /// <summary>The weekday; shown only.</summary>
        [BindNever]
        public DayOfWeek DayOfWeek { get; set; }

        /// <summary>The order within the day; shown only.</summary>
        [BindNever]
        public int SortOrder { get; set; }

        /// <summary>
        /// BR-11: a line that stays needs sets and reps, or a duration.
        /// </summary>
        /// <param name="validationContext">The validation context.</param>
        /// <returns>The error, if any.</returns>
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!Remove && !(Sets > 0 && Reps > 0) && !(DurationMinutes > 0))
            {
                yield return new ValidationResult(PlanExerciseViewModel.PrescriptionMessage, [nameof(Sets)]);
            }
        }
    }
}
