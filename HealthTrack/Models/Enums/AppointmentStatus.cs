using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models.Enums
{
    /// <summary>
    /// The appointment lifecycle of spec 1.6. Pending, Approved and Rescheduled can still change; the other five are final (BR-16).
    /// </summary>
    public enum AppointmentStatus
    {
        /// <summary>Booked by the client and waiting for the professional.</summary>
        [Display(Name = "Pending")]
        Pending = 1,

        /// <summary>Confirmed by the professional.</summary>
        [Display(Name = "Approved")]
        Approved = 2,

        /// <summary>Confirmed and moved to a new time by the professional.</summary>
        [Display(Name = "Rescheduled")]
        Rescheduled = 3,

        /// <summary>Turned down by the professional, with a reason (final).</summary>
        [Display(Name = "Rejected")]
        Rejected = 4,

        /// <summary>Cancelled by the client at least 2 hours before the start (final).</summary>
        [Display(Name = "Cancelled by client")]
        CancelledByClient = 5,

        /// <summary>Cancelled by the professional before the start, with a reason (final).</summary>
        [Display(Name = "Cancelled by trainer")]
        CancelledByTrainer = 6,

        /// <summary>Took place; the professional's notes are stored encrypted (final).</summary>
        [Display(Name = "Completed")]
        Completed = 7,

        /// <summary>The client did not attend (final).</summary>
        [Display(Name = "No-show")]
        NoShow = 8
    }
}
