namespace HealthTrack.Services
{
    /// <summary>
    /// The "Reminders" section of appsettings.json: how often <see cref="ReminderBackgroundService"/> runs (spec 1.8).
    /// </summary>
    public class ReminderOptions
    {
        /// <summary>The configuration section name.</summary>
        public const string SectionName = "Reminders";

        /// <summary>
        /// Whether the background service runs at all. When off, reminders still appear when a user's page polls, but no
        /// email copies are sent (they stay pending until it is switched on again, for up to 24 hours).
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Seconds between runs; 60 by default.</summary>
        public int IntervalSeconds { get; set; } = 60;
    }
}
