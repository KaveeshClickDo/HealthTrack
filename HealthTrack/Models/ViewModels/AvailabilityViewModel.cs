using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// The form for a weekly availability window (UC23): a weekday with a start and an end time.
    /// </summary>
    public class AvailabilityViewModel : IValidatableObject
    {
        /// <summary>The weekday.</summary>
        [Required(ErrorMessage = "Choose a day.")]
        [Display(Name = "Day")]
        public DayOfWeek? DayOfWeek { get; set; }

        /// <summary>When the window opens.</summary>
        [Required(ErrorMessage = "Enter a start time.")]
        [DataType(DataType.Time)]
        [Display(Name = "From")]
        public TimeSpan? StartTime { get; set; }

        /// <summary>When the window closes; later than the start.</summary>
        [Required(ErrorMessage = "Enter an end time.")]
        [DataType(DataType.Time)]
        [Display(Name = "To")]
        public TimeSpan? EndTime { get; set; }

        /// <summary>
        /// The window must end after it starts (spec 2.4).
        /// </summary>
        /// <param name="validationContext">The validation context.</param>
        /// <returns>The error, if any.</returns>
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (StartTime is { } start && EndTime is { } end && end <= start)
            {
                yield return new ValidationResult("The end time must be after the start time.", [nameof(EndTime)]);
            }
        }
    }
}
