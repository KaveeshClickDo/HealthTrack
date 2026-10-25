namespace HealthTrack.Models.ViewModels.Reports
{
    /// <summary>
    /// The engagement report (UC27): active clients, logs per client per week, logins per day, appointments by status and
    /// registrations per month; administrators also get the trainer workload.
    /// </summary>
    public class EngagementReportViewModel
    {
        /// <summary>The date range.</summary>
        public ReportRangeViewModel Range { get; init; } = new();

        /// <summary>Whether the report covers everyone (administrator) rather than one trainer's clients.</summary>
        public bool AllClients { get; init; }

        /// <summary>The clients in scope.</summary>
        public int ClientsInScope { get; init; }

        /// <summary>Clients with any log in the last 7 days of the range.</summary>
        public int ActiveClients { get; init; }

        /// <summary>The range week by week.</summary>
        public IReadOnlyList<EngagementWeekRow> Weeks { get; init; } = [];

        /// <summary>Appointments starting in the range, by status.</summary>
        public IReadOnlyList<CountRow> AppointmentsByStatus { get; init; } = [];

        /// <summary>Accounts registered in the range, by month.</summary>
        public IReadOnlyList<CountRow> RegistrationsByMonth { get; init; } = [];

        /// <summary>The trainer workload, for administrators only (empty for a trainer).</summary>
        public IReadOnlyList<TrainerWorkloadRow> TrainerWorkload { get; init; } = [];

        /// <summary>Logins per day (line chart).</summary>
        public ChartSeries Chart { get; init; } = new();
    }
}
