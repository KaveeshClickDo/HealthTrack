using HealthTrack.Helpers;
using HealthTrack.Models.ViewModels;
using HealthTrack.Services;
using Microsoft.AspNetCore.Mvc;

namespace HealthTrack.Areas.Admin.Controllers
{
    /// <summary>
    /// Approve or reject trainer registrations (UC30, BR-03).
    /// </summary>
    public class TrainerApprovalsController : AdminAreaController
    {
        private readonly IAccountService _accounts;

        /// <summary>
        /// Creates the controller.
        /// </summary>
        /// <param name="accounts">The account service.</param>
        public TrainerApprovalsController(IAccountService accounts)
        {
            _accounts = accounts;
        }

        /// <summary>
        /// The trainers waiting for approval, with everything needed to check them.
        /// </summary>
        /// <returns>The list.</returns>
        [HttpGet]
        public async Task<IActionResult> Index() => View(await _accounts.GetPendingTrainersAsync());

        /// <summary>
        /// Approves a trainer, who can then sign in.
        /// </summary>
        /// <param name="id">The trainer profile's key.</param>
        /// <returns>The list.</returns>
        [HttpPost]
        public async Task<IActionResult> Approve(int id)
        {
            try
            {
                if (!await _accounts.ApproveTrainerAsync(id, CurrentUserId))
                {
                    return NotFound();
                }

                SetSuccess("The trainer has been approved and can now log in.");
            }
            catch (BusinessRuleException ex)
            {
                SetError(ex.Message);
            }

            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Rejects a trainer registration; a reason is required.
        /// </summary>
        /// <param name="id">The trainer profile's key.</param>
        /// <param name="model">The reason.</param>
        /// <returns>The list.</returns>
        [HttpPost]
        public async Task<IActionResult> Reject(int id, RejectTrainerViewModel model)
        {
            if (!ModelState.IsValid)
            {
                SetError("Give a reason for the rejection (up to 400 characters).");
                return RedirectToAction(nameof(Index));
            }

            try
            {
                if (!await _accounts.RejectTrainerAsync(id, model.Reason, CurrentUserId))
                {
                    return NotFound();
                }

                SetSuccess("The trainer registration has been rejected.");
            }
            catch (BusinessRuleException ex)
            {
                SetError(ex.Message);
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
