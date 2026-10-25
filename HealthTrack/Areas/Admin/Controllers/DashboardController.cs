using HealthTrack.Helpers;
using HealthTrack.Services;
using Microsoft.AspNetCore.Mvc;

namespace HealthTrack.Areas.Admin.Controllers
{
    /// <summary>
    /// The administrator's landing page after login (UC28).
    /// </summary>
    public class DashboardController : AdminAreaController
    {
        private readonly IDashboardService _dashboards;
        private readonly IReminderService _reminders;
        private readonly IClock _clock;

        /// <summary>
        /// Creates the controller.
        /// </summary>
        /// <param name="dashboards">Builds the dashboard.</param>
        /// <param name="reminders">Creates the user's due reminders when the dashboard opens (UC34).</param>
        /// <param name="clock">Supplies the time.</param>
        public DashboardController(IDashboardService dashboards, IReminderService reminders, IClock clock)
        {
            _dashboards = dashboards;
            _reminders = reminders;
            _clock = clock;
        }

        /// <summary>
        /// The dashboard: users, approvals, assignments, usage charts and recent audit events.
        /// </summary>
        /// <returns>The dashboard page.</returns>
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            // Spec 1.8: opening the dashboard also checks for due reminders, so they show without waiting for the timer.
            await _reminders.GenerateDueRemindersAsync(CurrentUserId, _clock.Now);
            return View(await _dashboards.GetAdminDashboardAsync(CurrentUserId));
        }
    }
}
