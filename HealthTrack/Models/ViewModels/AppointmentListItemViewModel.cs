using HealthTrack.Models.Enums;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// One appointment in a list or at the top of its page, seen by the client or by its professional, with what each
    /// may do with it now.
    /// </summary>
    public class AppointmentListItemViewModel
    {
        /// <summary>The appointment's key.</summary>
        public int AppointmentId { get; init; }

        /// <summary>The start.</summary>
        public DateTime StartDateTime { get; init; }

        /// <summary>Length in minutes.</summary>
        public int DurationMinutes { get; init; }

        /// <summary>What it is for.</summary>
        public AppointmentType AppointmentType { get; init; }

        /// <summary>In person or online.</summary>
        public AppointmentMode Mode { get; init; }

        /// <summary>Where it is in its lifecycle.</summary>
        public AppointmentStatus Status { get; init; }

        /// <summary>The client's name.</summary>
        public string ClientName { get; init; } = string.Empty;

        /// <summary>The professional's name.</summary>
        public string TrainerName { get; init; } = string.Empty;

        /// <summary>The professional's type.</summary>
        public ProfessionalType ProfessionalType { get; init; }

        /// <summary>The room or meeting link, once approved.</summary>
        public string? Location { get; init; }

        /// <summary>The cancellation or rejection reason.</summary>
        public string? CancellationReason { get; init; }

        /// <summary>The client's reason for booking; decrypted only for the client and that professional (BR-20).</summary>
        public string? ClientReason { get; init; }

        /// <summary>The RowVersion, base64, for the action forms.</summary>
        public string RowVersion { get; init; } = string.Empty;

        /// <summary>Whether the viewer may cancel it now (client: until 2 hours before; trainer: confirmed and not started).</summary>
        public bool CanCancel { get; init; }

        /// <summary>The end: the start plus the length.</summary>
        public DateTime EndDateTime => StartDateTime.AddMinutes(DurationMinutes);

        /// <summary>Whether it can still change: Pending, Approved or Rescheduled (spec 1.6).</summary>
        public bool IsOpen => Status is AppointmentStatus.Pending or AppointmentStatus.Approved or AppointmentStatus.Rescheduled;
    }
}
