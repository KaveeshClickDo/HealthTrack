namespace HealthTrack.Helpers
{
    /// <summary>
    /// Supplies the current local time. Business code asks this instead of calling DateTime.Now, so tests can fix the clock.
    /// </summary>
    public interface IClock
    {
        /// <summary>The current local date and time.</summary>
        DateTime Now { get; }

        /// <summary>The current local date, with the time set to midnight.</summary>
        DateTime Today { get; }
    }
}
