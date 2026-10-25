namespace HealthTrack.Models.ViewModels.Reports
{
    /// <summary>
    /// One week (Monday to Sunday, cut to the range) of the engagement report (spec 1.7).
    /// </summary>
    public class EngagementWeekRow
    {
        /// <summary>The week's Monday.</summary>
        public DateTime WeekStart { get; init; }

        /// <summary>Clients with any workout, meal or measurement logged that week.</summary>
        public int ActiveClients { get; init; }

        /// <summary>Workout sessions logged.</summary>
        public int WorkoutLogs { get; init; }

        /// <summary>Meals logged.</summary>
        public int MealLogs { get; init; }

        /// <summary>Measurements recorded.</summary>
        public int Measurements { get; init; }

        /// <summary>All logs divided by the clients in scope, 1 dp.</summary>
        public decimal LogsPerClient { get; init; }

        /// <summary>Successful logins by the users in scope.</summary>
        public int Logins { get; init; }
    }
}
