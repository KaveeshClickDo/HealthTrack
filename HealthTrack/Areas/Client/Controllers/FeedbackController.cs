using HealthTrack.Services;
using Microsoft.AspNetCore.Mvc;

namespace HealthTrack.Areas.Client.Controllers
{
    /// <summary>
    /// The feedback the client's trainers have given (UC15).
    /// </summary>
    public class FeedbackController : ClientAreaController
    {
        private readonly IFeedbackService _feedback;

        /// <summary>
        /// Creates the controller.
        /// </summary>
        /// <param name="feedback">The feedback service.</param>
        public FeedbackController(IFeedbackService feedback)
        {
            _feedback = feedback;
        }

        /// <summary>
        /// The client's feedback, newest first, 20 per page.
        /// </summary>
        /// <param name="page">The page number.</param>
        /// <returns>The list.</returns>
        [HttpGet]
        public async Task<IActionResult> Index(int page = 1)
        {
            if (await CurrentClientIdAsync() is not { } clientId)
            {
                return NotFound();
            }

            return View(await _feedback.GetForClientAsync(clientId, page));
        }
    }
}
