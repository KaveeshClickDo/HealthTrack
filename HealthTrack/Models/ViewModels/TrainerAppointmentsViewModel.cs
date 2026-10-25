namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// The professional's appointments: requests waiting for a decision, then today and upcoming, then past.
    /// </summary>
    public class TrainerAppointmentsViewModel
    {
        /// <summary>Pending requests, soonest first.</summary>
        public IReadOnlyList<AppointmentListItemViewModel> Pending { get; init; } = [];

        /// <summary>Decided appointments from today on, soonest first.</summary>
        public IReadOnlyList<AppointmentListItemViewModel> Upcoming { get; init; } = [];

        /// <summary>Decided appointments before today, 20 per page, newest first.</summary>
        public PagedResult<AppointmentListItemViewModel> Past { get; init; } = new([], 1, 20, 0);

        /// <summary>Today's date, for marking today's rows.</summary>
        public DateTime Today { get; init; }
    }
}
