using HealthTrack.Helpers;
using HealthTrack.Services;
using Microsoft.AspNetCore.Mvc;

namespace HealthTrack.Areas.Client.Controllers
{
    /// <summary>
    /// The client's landing page after login (UC07).
    /// </summary>
    public class DashboardController : ClientAreaController
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
        /// The dashboard: health stats, today's workout and meals, appointments, feedback and notifications.
        /// </summary>
        /// <returns>The dashboard page.</returns>
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            // Spec 1.8: opening the dashboard also checks for due reminders, so they show without waiting for the timer.
            await _reminders.GenerateDueRemindersAsync(CurrentUserId, _clock.Now);
            var model = await _dashboards.GetClientDashboardAsync(CurrentUserId);
            return model is null ? NotFound() : View(model);
        }
    }
}
