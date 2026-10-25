namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// One appointment's page, for its client or its professional only (BR-08): the details, the encrypted reason and
    /// notes (BR-20), and the actions the viewer may take now.
    /// </summary>
    public class AppointmentDetailsViewModel
    {
        /// <summary>The appointment.</summary>
        public AppointmentListItemViewModel Appointment { get; init; } = new();

        /// <summary>Whether the viewer is the professional (otherwise the client).</summary>
        public bool ViewerIsTrainer { get; init; }

        /// <summary>The professional's consultation notes, once completed.</summary>
        public string? ProfessionalNotes { get; init; }

        /// <summary>When it was booked.</summary>
        public DateTime CreatedAt { get; init; }

        /// <summary>When it last changed.</summary>
        public DateTime? UpdatedAt { get; init; }

        /// <summary>The trainer can approve or reject: the request is pending.</summary>
        public bool CanDecide { get; init; }

        /// <summary>The trainer can reschedule: the appointment is confirmed and hasn't started.</summary>
        public bool CanReschedule { get; init; }

        /// <summary>The trainer can record the outcome: the appointment is confirmed and has started.</summary>
        public bool CanRecordOutcome { get; init; }

        /// <summary>The empty action form.</summary>
        public AppointmentActionViewModel Action { get; init; } = new();
    }
}
