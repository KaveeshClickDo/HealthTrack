using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// The small forms that move an appointment along its lifecycle (approve, reject, cancel, complete, no-show). Each
    /// posts the RowVersion it was shown, so a clashing change is caught (spec 1.6). Which fields an action needs is
    /// checked by the Appointment methods, for example a reason for a rejection (BR-16).
    /// </summary>
    public class AppointmentActionViewModel
    {
        /// <summary>The appointment's RowVersion as the page showed it, base64.</summary>
        [Required(ErrorMessage = "Reload the page and try again.")]
        public string RowVersion { get; set; } = string.Empty;

        /// <summary>The room, or the meeting link when online (approve).</summary>
        [StringLength(200)]
        [Display(Name = "Room or meeting link")]
        public string? Location { get; set; }

        /// <summary>Why the appointment is rejected or cancelled.</summary>
        [StringLength(500)]
        [Display(Name = "Reason")]
        public string? Reason { get; set; }

        /// <summary>The consultation notes (complete); stored encrypted (BR-20).</summary>
        [StringLength(4000)]
        [Display(Name = "Consultation notes")]
        public string? Notes { get; set; }
    }
}
