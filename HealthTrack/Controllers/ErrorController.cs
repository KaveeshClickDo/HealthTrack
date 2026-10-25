using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthTrack.Controllers
{
    /// <summary>
    /// Friendly error pages for UseStatusCodePagesWithReExecute("/Error/{0}") and UseExceptionHandler("/Error/ServerError").
    /// Open to everyone, without an anti-forgery check and for any HTTP method, because a re-executed request keeps the
    /// original method (a failed POST is re-executed as a POST). No exception details are ever shown.
    /// </summary>
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public class ErrorController : BaseController
    {
        /// <summary>
        /// The page was not found, or the user may not see it (ownership failures answer 404 so IDs can't be probed).
        /// Named PageNotFound because a method called NotFound would hide ControllerBase.NotFound().
        /// </summary>
        /// <returns>The 404 page.</returns>
        [Route("Error/NotFound")]
        [Route("Error/404")]
        [ActionName("NotFound")]
        public IActionResult PageNotFound()
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return View("NotFound");
        }

        /// <summary>
        /// The signed-in user's role may not open the page (the cookie's AccessDeniedPath).
        /// </summary>
        /// <returns>The 403 page.</returns>
        [Route("Error/Forbidden")]
        [Route("Error/403")]
        public IActionResult Forbidden()
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return View();
        }

        /// <summary>
        /// Any other error: 500 from the exception handler, 400 (for example a failed anti-forgery check) and the rest.
        /// </summary>
        /// <param name="code">The status code from the URL; 500 when absent.</param>
        /// <returns>The error page.</returns>
        [Route("Error/ServerError")]
        [Route("Error/{code:int}")]
        public IActionResult ServerError(int? code)
        {
            var statusCode = code is >= 400 and <= 599 ? code.Value : StatusCodes.Status500InternalServerError;
            Response.StatusCode = statusCode;
            ViewData["StatusCode"] = statusCode;
            return View();
        }
    }
}
