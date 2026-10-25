namespace HealthTrack.Models.ViewModels.Reports
{
    /// <summary>
    /// The bar above a report (Views/Shared/_ReportToolbar): the date-range form, Export CSV and Print.
    /// </summary>
    public class ReportToolbarViewModel
    {
        /// <summary>The range shown.</summary>
        public ReportRangeViewModel Range { get; init; } = new();

        /// <summary>The report's key for the export link, for example client-performance.</summary>
        public string ReportKey { get; init; } = string.Empty;
    }
}
