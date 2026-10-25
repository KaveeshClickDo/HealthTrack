using HealthTrack.Helpers;
using HealthTrack.Models.ViewModels.Reports;
using HealthTrack.Services;
using Microsoft.AspNetCore.Mvc;

namespace HealthTrack.Areas.Trainer.Controllers
{
    /// <summary>
    /// The trainer's reports (UC27) and their CSV export (UC40), covering only the clients assigned to them (BR-08). The
    /// views are shared with the administrator's reports (Views/Shared/*Report.cshtml).
    /// </summary>
    public class ReportsController : TrainerAreaController
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
        /// Client performance: weight and BMI, workouts, adherence, eating against target, calories burned, appointments.
        /// </summary>
        /// <param name="from">The first day; 30 days ago by default.</param>
        /// <param name="to">The last day; today by default.</param>
        /// <returns>The report.</returns>
        [HttpGet]
        public async Task<IActionResult> ClientPerformance(DateTime? from, DateTime? to) =>
            View("ClientPerformanceReport", await _reports.GetClientPerformanceAsync(Range(from, to), CurrentUserId));

        /// <summary>
        /// Popular routines: templates by use, the top 10 exercises and the exercise categories.
        /// </summary>
        /// <param name="from">The first day; 30 days ago by default.</param>
        /// <param name="to">The last day; today by default.</param>
        /// <returns>The report.</returns>
        [HttpGet]
        public async Task<IActionResult> PopularRoutines(DateTime? from, DateTime? to) =>
            View("PopularRoutinesReport", await _reports.GetPopularRoutinesAsync(Range(from, to), CurrentUserId));

        /// <summary>
        /// Engagement: active clients, logs per client per week, logins, appointments by status and registrations.
        /// </summary>
        /// <param name="from">The first day; 30 days ago by default.</param>
        /// <param name="to">The last day; today by default.</param>
        /// <returns>The report.</returns>
        [HttpGet]
        public async Task<IActionResult> Engagement(DateTime? from, DateTime? to) =>
            View("EngagementReport", await _reports.GetEngagementAsync(Range(from, to), CurrentUserId));

        /// <summary>
        /// Downloads a report's table as CSV (UC40); the export is audited.
        /// </summary>
        /// <param name="report">client-performance, popular-routines or engagement.</param>
        /// <param name="from">The first day.</param>
        /// <param name="to">The last day.</param>
        /// <returns>The CSV file, or 404 for an unknown report.</returns>
        [HttpGet]
        public async Task<IActionResult> ExportCsv(string? report, DateTime? from, DateTime? to)
        {
            var file = await _reports.ExportCsvAsync(report ?? string.Empty, Range(from, to), CurrentUserId, CurrentUserId);
            return file is null ? NotFound() : File(file.Content, file.ContentType, file.FileName);
        }

        private ReportRangeViewModel Range(DateTime? from, DateTime? to) => ReportRangeViewModel.Resolve(from, to, _clock.Today);
    }
}
