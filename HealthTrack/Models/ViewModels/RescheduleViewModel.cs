using System.ComponentModel.DataAnnotations;
using HealthTrack.Models.Entities;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// The trainer's reschedule form (UC25): a day, a length and one of that day's free start times, which leave out
    /// the appointment's own slot so it can move within it.
    /// </summary>
    public class RescheduleViewModel : IValidatableObject
    {
        /// <summary>The appointment's RowVersion as the page showed it, base64.</summary>
        [Required(ErrorMessage = "Reload the page and try again.")]
        public string RowVersion { get; set; } = string.Empty;

        /// <summary>The new day.</summary>
        [Required(ErrorMessage = "Choose a date.")]
        [DataType(DataType.Date)]
        [Display(Name = "New date")]
        public DateTime? Date { get; set; }

        /// <summary>The new length: 30, 45, 60 or 90 minutes.</summary>
        [Required(ErrorMessage = "Choose a length.")]
        [Display(Name = "Length")]
        public int? DurationMinutes { get; set; }

        /// <summary>One of the free start times for the day.</summary>
        [Required(ErrorMessage = "Choose one of the free times.")]
        [Display(Name = "New time")]
        public TimeSpan? StartTime { get; set; }

        /// <summary>The appointment as it stands; shown only.</summary>
        [BindNever]
        [ValidateNever]
        public AppointmentListItemViewModel Appointment { get; set; } = new();

        /// <summary>The free start times for the chosen day and length; filled by the service.</summary>
        [BindNever]
        [ValidateNever]
        public IReadOnlyList<TimeSpan> Slots { get; set; } = [];

        /// <summary>The first day that can be chosen (today).</summary>
        [BindNever]
        public DateTime EarliestDate { get; set; }

        /// <summary>The last day that can be chosen (60 days ahead, BR-15).</summary>
        [BindNever]
        public DateTime LatestDate { get; set; }

        /// <summary>The new start, or null until both the date and the time are given.</summary>
        public DateTime? NewStart => Date is { } date && StartTime is { } time ? date.Date + time : null;

        /// <summary>
        /// The length must be one the spec allows (spec 1.6).
        /// </summary>
        /// <param name="validationContext">The validation context.</param>
        /// <returns>The error, if any.</returns>
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (DurationMinutes is { } minutes && !Entities.Appointment.AllowedDurations.Contains(minutes))
            {
                yield return new ValidationResult("Appointments last 30, 45, 60 or 90 minutes.", [nameof(DurationMinutes)]);
            }
        }
    }
}
