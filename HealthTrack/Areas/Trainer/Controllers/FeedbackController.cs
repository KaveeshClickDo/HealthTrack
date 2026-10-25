using HealthTrack.Models.ViewModels;
using HealthTrack.Services;
using Microsoft.AspNetCore.Mvc;

namespace HealthTrack.Areas.Trainer.Controllers
{
    /// <summary>
    /// Give feedback to assigned clients (UC22).
    /// </summary>
    public class FeedbackController : TrainerAreaController
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
        /// The feedback form, optionally for one client and one of their sessions.
        /// </summary>
        /// <param name="clientId">The client.</param>
        /// <param name="workoutLogId">The session the feedback is about.</param>
        /// <returns>The form, or 404 when the client or session isn't the trainer's to see.</returns>
        [HttpGet]
        public async Task<IActionResult> Create(int? clientId, int? workoutLogId)
        {
            var model = await _feedback.GetFeedbackFormAsync(CurrentUserId, clientId, workoutLogId);
            return model is null ? NotFound() : View(model);
        }

        /// <summary>
        /// Saves the feedback and notifies the client.
        /// </summary>
        /// <param name="model">The form.</param>
        /// <returns>The client list, or the form with its errors.</returns>
        [HttpPost]
        public async Task<IActionResult> Create(FeedbackViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await _feedback.FillFeedbackOptionsAsync(model, CurrentUserId);
                return View(model);
            }

            if (await _feedback.GiveFeedbackAsync(CurrentUserId, model) is null)
            {
                return NotFound();
            }

            SetSuccess("Your feedback has been sent. The client has been notified.");
            return RedirectToAction("Index", "Clients");
        }
    }
}
