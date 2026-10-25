using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthTrack.Controllers
{
    /// <summary>
    /// The public landing page and privacy notice.
    /// </summary>
    [AllowAnonymous]
    public class HomeController : BaseController
    {
        /// <summary>
        /// The landing page; signed-in users go straight to their dashboard.
        /// </summary>
        /// <returns>The landing page or a redirect.</returns>
        [HttpGet]
        public IActionResult Index() => CurrentRole() is { } role ? RedirectToDashboard(role) : View();

        /// <summary>
        /// The privacy notice the registration consent links to (BR-02).
        /// </summary>
        /// <returns>The privacy notice.</returns>
        [HttpGet]
        public IActionResult Privacy() => View();
    }
}
