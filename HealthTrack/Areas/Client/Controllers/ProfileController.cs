using HealthTrack.Models.ViewModels;
using HealthTrack.Services;
using Microsoft.AspNetCore.Mvc;

namespace HealthTrack.Areas.Client.Controllers
{
    /// <summary>
    /// The client's health profile: goal, activity, target weight, medical details and whether the trainer sees them.
    /// Data export and account deletion arrive in Phase 9.
    /// </summary>
    public class ProfileController : ClientAreaController
    {
        private readonly IAccountService _accounts;

        /// <summary>
        /// Creates the controller.
        /// </summary>
        /// <param name="accounts">The account service.</param>
        public ProfileController(IAccountService accounts)
        {
            _accounts = accounts;
        }

        /// <summary>
        /// The health profile form.
        /// </summary>
        /// <returns>The form.</returns>
        [HttpGet]
        public async Task<IActionResult> Edit()
        {
            var model = await _accounts.GetHealthProfileAsync(CurrentUserId);
            return model is null ? NotFound() : View(model);
        }

        /// <summary>
        /// Saves the health profile.
        /// </summary>
        /// <param name="model">The form.</param>
        /// <returns>The form again.</returns>
        [HttpPost]
        public async Task<IActionResult> Edit(HealthProfileViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (!await _accounts.UpdateHealthProfileAsync(CurrentUserId, model))
            {
                return NotFound();
            }

            SetSuccess("Your health profile has been saved.");
            return RedirectToAction(nameof(Edit));
        }
    }
}
