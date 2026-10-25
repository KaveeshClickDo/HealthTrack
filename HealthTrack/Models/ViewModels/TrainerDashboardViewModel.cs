namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// The trainer's dashboard (UC17, spec 1.4): assigned clients with activity and adherence, pending requests and today's
    /// schedule.
    /// </summary>
    public class TrainerDashboardViewModel
    {
        /// <summary>The trainer's first name, for the greeting.</summary>
        public string FirstName { get; init; } = string.Empty;

        /// <summary>Today's date.</summary>
        public DateTime Today { get; init; }

        /// <summary>The actively assigned clients, inactive ones first.</summary>
        public IReadOnlyList<TrainerClientViewModel> Clients { get; init; } = [];

        /// <summary>How many clients have done nothing for 7 days or more.</summary>
        public int InactiveClients => Clients.Count(c => c.IsInactive);

        /// <summary>Appointment requests waiting for a decision, soonest first.</summary>
        public IReadOnlyList<AppointmentListItemViewModel> PendingRequests { get; init; } = [];

        /// <summary>Today's confirmed and held appointments, in time order.</summary>
        public IReadOnlyList<AppointmentListItemViewModel> TodaysSchedule { get; init; } = [];
    }
}
