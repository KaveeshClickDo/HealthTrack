using HealthTrack.Models.ViewModels.Reports;

namespace HealthTrack.Services
{
    /// <summary>
    /// The reports of spec 1.7 (UC27) and their CSV export (UC40). Each takes a date range and an optional trainer scope:
    /// with a trainer's user ID only that trainer's assigned clients (and their own appointments) count (BR-08); without
    /// one, everyone does (administrators).
    /// </summary>
    public interface IReportService
    {
        /// <summary>
        /// Client performance: start vs latest weight and BMI, workouts completed, adherence, average daily calories vs
        /// target, calories burned and appointments attended, per client.
        /// </summary>
        /// <param name="range">The date range.</param>
        /// <param name="trainerUserId">The trainer whose clients to include; null for everyone.</param>
        /// <returns>The report.</returns>
        Task<ClientPerformanceReportViewModel> GetClientPerformanceAsync(ReportRangeViewModel range, string? trainerUserId = null);

        /// <summary>
        /// Popular routines: templates ranked by clients assigned, active plans and adherence; the top 10 exercises logged;
        /// each exercise category's share.
        /// </summary>
        /// <param name="range">The date range.</param>
        /// <param name="trainerUserId">The trainer whose clients to include; null for everyone.</param>
        /// <returns>The report.</returns>
        Task<PopularRoutinesReportViewModel> GetPopularRoutinesAsync(ReportRangeViewModel range, string? trainerUserId = null);

        /// <summary>
        /// Engagement: active clients, logs per client per week, logins per day, appointments by status and registrations
        /// per month; for everyone (administrators) also the trainer workload.
        /// </summary>
        /// <param name="range">The date range.</param>
        /// <param name="trainerUserId">The trainer whose clients to include; null for everyone.</param>
        /// <returns>The report.</returns>
        Task<EngagementReportViewModel> GetEngagementAsync(ReportRangeViewModel range, string? trainerUserId = null);

        /// <summary>
        /// Trainer workload (administrators only): each approved professional's active clients against capacity, and their
        /// appointments, plans and feedback in the range.
        /// </summary>
        /// <param name="range">The date range.</param>
        /// <returns>The report.</returns>
        Task<TrainerWorkloadReportViewModel> GetTrainerWorkloadAsync(ReportRangeViewModel range);

        /// <summary>
        /// Exports a report's main table as CSV (UC40) and audits the export.
        /// </summary>
        /// <param name="report">client-performance, popular-routines, engagement or (everyone only) trainer-workload.</param>
        /// <param name="range">The date range.</param>
        /// <param name="trainerUserId">The trainer whose clients to include; null for everyone.</param>
        /// <param name="actingUserId">The user exporting, for the audit log.</param>
        /// <returns>The file, or null for an unknown report or one the scope may not export.</returns>
        Task<ReportFile?> ExportCsvAsync(string report, ReportRangeViewModel range, string? trainerUserId, string actingUserId);
    }
}
