using System.ComponentModel.DataAnnotations;
using HealthTrack.Security;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// The workout log form (UC09): today's planned session pre-filled from the active plan, or a free workout.
    /// </summary>
    public class LogWorkoutViewModel : IValidatableObject
    {
        /// <summary>The message when nothing is ticked (BR-13).</summary>
        public const string NoEntriesMessage = "Tick at least one exercise you did.";

        /// <summary>The plan the session follows; null for a free workout. The service checks it is the client's own.</summary>
        public int? WorkoutPlanId { get; set; }

        /// <summary>The plan's title, shown on today's workout.</summary>
        [StringLength(100)]
        public string? PlanTitle { get; set; }

        /// <summary>The day of the session; not in the future (BR-13).</summary>
        [Required(ErrorMessage = "Choose the date.")]
        [NotInFuture]
        [DataType(DataType.Date)]
        [Display(Name = "Date")]
        public DateTime? WorkoutDate { get; set; }

        /// <summary>Rating of perceived exertion, 1–10.</summary>
        [Range(1, 10, ErrorMessage = "Effort must be between 1 and 10.")]
        [Display(Name = "Effort (1–10)")]
        public int? PerceivedEffort { get; set; }

        /// <summary>The client's notes.</summary>
        [StringLength(500)]
        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        /// <summary>The exercise rows.</summary>
        public List<LogEntryViewModel> Entries { get; set; } = [];

        /// <summary>The exercises to choose from, grouped by category; filled by the service, never posted.</summary>
        [BindNever]
        [ValidateNever]
        public IReadOnlyList<SelectOptionViewModel> ExerciseOptions { get; set; } = [];

        /// <summary>Whether the client has an active plan (today's page only).</summary>
        [BindNever]
        public bool HasActivePlan { get; set; }

        /// <summary>Whether the active plan schedules nothing today (today's page only).</summary>
        [BindNever]
        public bool IsRestDay { get; set; }

        /// <summary>Whether a session was already logged today (today's page only).</summary>
        [BindNever]
        public bool AlreadyLoggedToday { get; set; }

        /// <summary>
        /// BR-13: a workout log needs at least one exercise.
        /// </summary>
        /// <param name="validationContext">The validation context.</param>
        /// <returns>The error, if any.</returns>
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!Entries.Any(e => e.Include))
            {
                yield return new ValidationResult(NoEntriesMessage);
            }
        }
    }
}
