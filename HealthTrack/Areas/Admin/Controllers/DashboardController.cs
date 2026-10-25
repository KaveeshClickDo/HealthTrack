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

        /// <summary>
        /// Creates the controller.
        /// </summary>
        /// <param name="accounts">The account service, for the greeting.</param>
        public DashboardController(IAccountService accounts)
        {
            _accounts = accounts;
        }

        /// <summary>
        /// The dashboard.
        /// </summary>
        /// <returns>The dashboard page.</returns>
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var summary = await _accounts.GetAccountSummaryAsync(CurrentUserId);
            return summary is null ? NotFound() : View(summary);
        }
    }
}
