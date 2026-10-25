namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// One existing availability window on the trainer's availability page.
    /// </summary>
    public class AvailabilityWindowViewModel
    {
        /// <summary>The window's key, for removing it.</summary>
        public int AvailabilityId { get; init; }

        /// <summary>The weekday.</summary>
        public DayOfWeek DayOfWeek { get; init; }

        /// <summary>When the window opens.</summary>
        public TimeSpan StartTime { get; init; }

        /// <summary>When the window closes.</summary>
        public TimeSpan EndTime { get; init; }
    }
}
