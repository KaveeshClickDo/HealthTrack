namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// Workout adherence (spec 1.10): scheduled days with a logged session ÷ days scheduled by the plan × 100.
    /// </summary>
    public class AdherenceViewModel
    {
        /// <summary>Days in the range on which the plan in effect scheduled training (up to today).</summary>
        public int ScheduledDays { get; init; }

        /// <summary>Scheduled days on which a session was logged.</summary>
        public int CompletedDays { get; init; }

        /// <summary>The percentage, rounded; null when nothing was scheduled.</summary>
        public int? Percent => ScheduledDays == 0
            ? null
            : (int)Math.Round(CompletedDays * 100m / ScheduledDays, MidpointRounding.AwayFromZero);
    }
}
