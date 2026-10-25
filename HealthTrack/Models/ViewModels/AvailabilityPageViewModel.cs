namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// The trainer's weekly availability: the windows by weekday (Monday first) and the add-window form.
    /// </summary>
    public class AvailabilityPageViewModel
    {
        /// <summary>The windows, Monday to Sunday, by start time.</summary>
        public IReadOnlyList<AvailabilityWindowViewModel> Windows { get; init; } = [];

        /// <summary>The add-window form.</summary>
        public AvailabilityViewModel NewWindow { get; set; } = new();

        /// <summary>The weekly hours on offer.</summary>
        public double WeeklyHours => Windows.Sum(w => (w.EndTime - w.StartTime).TotalHours);
    }
}
