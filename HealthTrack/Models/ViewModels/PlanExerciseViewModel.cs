using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// The form for adding one exercise to a plan on one weekday. It needs sets and reps, or a duration (BR-11).
    /// </summary>
    public class PlanExerciseViewModel : IValidatableObject
    {
        /// <summary>The message for a missing prescription (BR-11).</summary>
        public const string PrescriptionMessage = "Give sets and reps, or a duration.";

        /// <summary>The weekday.</summary>
        [Required(ErrorMessage = "Choose a day.")]
        [Display(Name = "Day")]
        public DayOfWeek? DayOfWeek { get; set; }

        /// <summary>The exercise from the library.</summary>
        [Required(ErrorMessage = "Choose an exercise.")]
        [Display(Name = "Exercise")]
        public int? ExerciseId { get; set; }

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

        /// <summary>
        /// BR-11: sets and reps together, or a duration.
        /// </summary>
        /// <param name="validationContext">The validation context.</param>
        /// <returns>The error, if any.</returns>
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!(Sets > 0 && Reps > 0) && !(DurationMinutes > 0))
            {
                yield return new ValidationResult(PrescriptionMessage, [nameof(Sets)]);
            }
        }
    }
}
