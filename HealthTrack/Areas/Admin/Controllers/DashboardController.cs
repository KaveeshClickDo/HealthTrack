using HealthTrack.Helpers;
using HealthTrack.Services;
using Microsoft.AspNetCore.Mvc;

namespace HealthTrack.Areas.Admin.Controllers
{
    /// <summary>
    /// The administrator's landing page after login.
    /// </summary>
    public class DashboardController : AdminAreaController
    {
        private readonly IAccountService _accounts;
        private readonly IReminderService _reminders;
        private readonly IClock _clock;

        /// <summary>
        /// Creates the controller.
        /// </summary>
        /// <param name="accounts">The account service, for the greeting.</param>
        /// <param name="reminders">Creates the user's due reminders when the dashboard opens (UC34).</param>
        /// <param name="clock">Supplies the time.</param>
        public DashboardController(IAccountService accounts, IReminderService reminders, IClock clock)
        {
            _accounts = accounts;
            _reminders = reminders;
            _clock = clock;
        }

        /// <summary>
        /// The dashboard.
        /// </summary>
        /// <returns>The dashboard page.</returns>
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            // Spec 1.8: opening the dashboard also checks for due reminders, so they show without waiting for the timer.
            await _reminders.GenerateDueRemindersAsync(CurrentUserId, _clock.Now);
            var summary = await _accounts.GetAccountSummaryAsync(CurrentUserId);
            return summary is null ? NotFound() : View(summary);
        }
    }
}
