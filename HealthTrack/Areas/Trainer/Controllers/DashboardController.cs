using HealthTrack.Helpers;
using HealthTrack.Services;
using Microsoft.AspNetCore.Mvc;

namespace HealthTrack.Areas.Trainer.Controllers
{
    /// <summary>
    /// The trainer's landing page after login (UC17).
    /// </summary>
    public class DashboardController : TrainerAreaController
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
        /// The dashboard: assigned clients with activity and adherence, pending requests, today's schedule and quick actions.
        /// </summary>
        /// <returns>The dashboard page.</returns>
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            // Spec 1.8: opening the dashboard also checks for due reminders, so they show without waiting for the timer.
            await _reminders.GenerateDueRemindersAsync(CurrentUserId, _clock.Now);
            var model = await _dashboards.GetTrainerDashboardAsync(CurrentUserId);
            return model is null ? NotFound() : View(model);
        }
    }
}
