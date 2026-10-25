namespace HealthTrack.Models.ViewModels.Reports
{
    /// <summary>
    /// The client performance report (UC27): one row per client in scope, and adherence as a chart.
    /// </summary>
    public class ClientPerformanceReportViewModel
    {
        /// <summary>The date range.</summary>
        public ReportRangeViewModel Range { get; init; } = new();

        /// <summary>Whether the report covers everyone (administrator) rather than one trainer's clients.</summary>
        public bool AllClients { get; init; }

        /// <summary>The clients, by name.</summary>
        public IReadOnlyList<ClientPerformanceRow> Rows { get; init; } = [];

        /// <summary>Workout adherence per client (bar chart).</summary>
        public ChartSeries Chart { get; init; } = new();
    }
}
