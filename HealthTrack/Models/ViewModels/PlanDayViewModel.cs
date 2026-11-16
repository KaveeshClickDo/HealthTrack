namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// One weekday of a plan: its exercises in order, or a rest day.
    /// </summary>
    public class PlanDayViewModel
    {
        /// <summary>The weekday.</summary>
        public DayOfWeek DayOfWeek { get; init; }

        /// <summary>The day's exercises in order.</summary>
        public IReadOnlyList<PlanLineViewModel> Lines { get; init; } = [];

        /// <summary>Whether nothing is planned that day.</summary>
        public bool IsRestDay => Lines.Count == 0;

        /// <summary>Whether this is today, for highlighting.</summary>
        public bool IsToday { get; init; }

        /// <summary>The day's date in the current week, on the client's own plan; null on a template preview.</summary>
        public DateTime? Date { get; init; }

        /// <summary>Whether the client logged a workout on that date.</summary>
        public bool IsLogged { get; init; }
    }
}
