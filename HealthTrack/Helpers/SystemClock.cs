namespace HealthTrack.Helpers
{
    /// <summary>
    /// The real clock. The app runs in one time zone and stores local times, so this returns local time.
    /// </summary>
    public class SystemClock : IClock
    {
        /// <inheritdoc />
        public DateTime Now => DateTime.Now;

        /// <inheritdoc />
        public DateTime Today => DateTime.Today;
    }
}
