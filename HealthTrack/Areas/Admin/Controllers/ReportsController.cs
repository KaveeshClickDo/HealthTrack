using HealthTrack.Helpers;
using HealthTrack.Models.ViewModels.Reports;
using HealthTrack.Services;
using Microsoft.AspNetCore.Mvc;

namespace HealthTrack.Areas.Admin.Controllers
{
    /// <summary>
    /// The administrator's reports (UC27) and their CSV export (UC40), covering everyone, plus the trainer workload. The
    /// views are shared with the trainer's reports (Views/Shared/*Report.cshtml).
    /// </summary>
    public class ReportsController : AdminAreaController
    {
        private readonly IReportService _reports;
        private readonly IClock _clock;

        /// <summary>
        /// Creates the controller.
        /// </summary>
        /// <param name="reports">The report service.</param>
        /// <param name="clock">Supplies today's date for the default range.</param>
        public ReportsController(IReportService reports, IClock clock)
        {
            _reports = reports;
            _clock = clock;
        }

        /// <summary>
        /// Client performance for every client.
        /// </summary>
        /// <param name="from">The first day; 30 days ago by default.</param>
        /// <param name="to">The last day; today by default.</param>
        /// <returns>The report.</returns>
        [HttpGet]
        public async Task<IActionResult> ClientPerformance(DateTime? from, DateTime? to) =>
            View("ClientPerformanceReport", await _reports.GetClientPerformanceAsync(Range(from, to)));

        /// <summary>
        /// Popular routines across all clients.
        /// </summary>
        /// <param name="from">The first day; 30 days ago by default.</param>
        /// <param name="to">The last day; today by default.</param>
        /// <returns>The report.</returns>
        [HttpGet]
        public async Task<IActionResult> PopularRoutines(DateTime? from, DateTime? to) =>
            View("PopularRoutinesReport", await _reports.GetPopularRoutinesAsync(Range(from, to)));

        /// <summary>
        /// Engagement across all users, with the trainer workload.
        /// </summary>
        /// <param name="from">The first day; 30 days ago by default.</param>
        /// <param name="to">The last day; today by default.</param>
        /// <returns>The report.</returns>
        [HttpGet]
        public async Task<IActionResult> Engagement(DateTime? from, DateTime? to) =>
            View("EngagementReport", await _reports.GetEngagementAsync(Range(from, to)));

        /// <summary>
        /// Each approved professional's clients against capacity, and their appointments, plans and feedback.
        /// </summary>
        /// <param name="from">The first day; 30 days ago by default.</param>
        /// <param name="to">The last day; today by default.</param>
        /// <returns>The report.</returns>
        [HttpGet]
        public async Task<IActionResult> TrainerWorkload(DateTime? from, DateTime? to) =>
            View("TrainerWorkloadReport", await _reports.GetTrainerWorkloadAsync(Range(from, to)));

        /// <summary>
        /// Downloads a report's table as CSV (UC40); the export is audited.
        /// </summary>
        /// <param name="report">client-performance, popular-routines, engagement or trainer-workload.</param>
        /// <param name="from">The first day.</param>
        /// <param name="to">The last day.</param>
        /// <returns>The CSV file, or 404 for an unknown report.</returns>
        [HttpGet]
        public async Task<IActionResult> ExportCsv(string? report, DateTime? from, DateTime? to)
        {
            var file = await _reports.ExportCsvAsync(report ?? string.Empty, Range(from, to), null, CurrentUserId);
            return file is null ? NotFound() : File(file.Content, file.ContentType, file.FileName);
        }

        private ReportRangeViewModel Range(DateTime? from, DateTime? to) => ReportRangeViewModel.Resolve(from, to, _clock.Today);
    }
}
