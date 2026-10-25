using System.ComponentModel.DataAnnotations;
using HealthTrack.Models.Entities;
using HealthTrack.Models.Enums;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// The booking form (UC13). The client is never on the form: it is always the signed-in user (spec 3.2).
    /// </summary>
    public class BookAppointmentViewModel : IValidatableObject
    {
        /// <summary>The professional; an approved, active trainer.</summary>
        [Required(ErrorMessage = "Choose a professional.")]
        [Display(Name = "Professional")]
        public int? TrainerId { get; set; }

        /// <summary>What the appointment is for.</summary>
        [Required(ErrorMessage = "Choose the type of appointment.")]
        [Display(Name = "Type")]
        public AppointmentType? AppointmentType { get; set; }

        /// <summary>In person or online.</summary>
        [Required(ErrorMessage = "Choose in person or online.")]
        [Display(Name = "Where")]
        public AppointmentMode? Mode { get; set; }

        /// <summary>The day.</summary>
        [Required(ErrorMessage = "Choose a date.")]
        [DataType(DataType.Date)]
        [Display(Name = "Date")]
        public DateTime? Date { get; set; }

        /// <summary>Length in minutes: 30, 45, 60 or 90.</summary>
        [Required(ErrorMessage = "Choose a length.")]
        [Display(Name = "Length")]
        public int? DurationMinutes { get; set; } = 60;

        /// <summary>One of the free start times offered for the day (BR-15).</summary>
        [Required(ErrorMessage = "Choose one of the free times.")]
        [Display(Name = "Time")]
        public TimeSpan? StartTime { get; set; }

        /// <summary>Why the client is booking; stored encrypted (BR-20).</summary>
        [StringLength(1000)]
        [Display(Name = "Reason")]
        public string? ClientReason { get; set; }

        /// <summary>The professionals the client can book; filled by the service.</summary>
        [BindNever]
        [ValidateNever]
        public IReadOnlyList<ProfessionalOptionViewModel> Professionals { get; set; } = [];

        /// <summary>The free start times for the chosen day, when re-showing the form; filled by the service.</summary>
        [BindNever]
        [ValidateNever]
        public IReadOnlyList<TimeSpan> Slots { get; set; } = [];

        /// <summary>The first day that can be booked (today; BR-15 then needs 2 hours' notice).</summary>
        [BindNever]
        public DateTime EarliestDate { get; set; }

        /// <summary>The last day that can be booked (60 days ahead, BR-15).</summary>
        [BindNever]
        public DateTime LatestDate { get; set; }

        /// <summary>The chosen start, or null until both the date and the time are given.</summary>
        public DateTime? StartDateTime => Date is { } date && StartTime is { } time ? date.Date + time : null;

        /// <summary>
        /// The length must be one the spec allows (spec 1.6).
        /// </summary>
        /// <param name="validationContext">The validation context.</param>
        /// <returns>The error, if any.</returns>
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (DurationMinutes is { } minutes && !Appointment.AllowedDurations.Contains(minutes))
            {
                yield return new ValidationResult("Appointments last 30, 45, 60 or 90 minutes.", [nameof(DurationMinutes)]);
            }
        }
    }
}
