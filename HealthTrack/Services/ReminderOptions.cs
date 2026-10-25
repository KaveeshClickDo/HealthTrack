namespace HealthTrack.Services
{
    /// <summary>
    /// The "Reminders" section of appsettings.json: how often <see cref="ReminderBackgroundService"/> runs (spec 1.8).
    /// </summary>
    public class ReminderOptions
    {
        /// <summary>The configuration section name.</summary>
        public const string SectionName = "Reminders";

        /// <summary>Whether the background service runs at all. Reminders still appear when a user's page polls.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Seconds between runs; 60 by default.</summary>
        public int IntervalSeconds { get; set; } = 60;
    }
}
