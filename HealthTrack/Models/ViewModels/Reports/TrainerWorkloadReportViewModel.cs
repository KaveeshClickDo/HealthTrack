namespace HealthTrack.Models.ViewModels.Reports
{
    /// <summary>
    /// The trainer workload report (administrators, UC27): each professional's clients against capacity and their activity
    /// in the range.
    /// </summary>
    public class TrainerWorkloadReportViewModel
    {
        /// <summary>The date range.</summary>
        public ReportRangeViewModel Range { get; init; } = new();

        /// <summary>The approved professionals, by name.</summary>
        public IReadOnlyList<TrainerWorkloadRow> Rows { get; init; } = [];

        /// <summary>Active clients per professional with their capacity (bar chart and reference line).</summary>
        public ChartSeries Chart { get; init; } = new();
    }
}
