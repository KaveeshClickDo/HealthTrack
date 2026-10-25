namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// The client's appointments: open ones from today on, then the rest, newest first.
    /// </summary>
    public class ClientAppointmentsViewModel
    {
        /// <summary>Pending, approved and rescheduled appointments from today on, soonest first.</summary>
        public IReadOnlyList<AppointmentListItemViewModel> Upcoming { get; init; } = [];

        /// <summary>Earlier and closed appointments, 20 per page, newest first.</summary>
        public PagedResult<AppointmentListItemViewModel> Past { get; init; } = new([], 1, 20, 0);
    }
}
