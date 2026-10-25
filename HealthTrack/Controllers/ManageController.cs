using HealthTrack.Models.ViewModels;
using HealthTrack.Services;
using Microsoft.AspNetCore.Mvc;

namespace HealthTrack.Controllers
{
    /// <summary>
    /// The signed-in user's own account: summary, personal details and password (UC05). Open to every role.
    /// </summary>
    public class ManageController : BaseController
    {
        private readonly IAccountService _accounts;

        /// <summary>
        /// Creates the controller.
        /// </summary>
        /// <param name="accounts">The account service.</param>
        public ManageController(IAccountService accounts)
        {
            _accounts = accounts;
        }

        /// <summary>
        /// The account summary.
        /// </summary>
        /// <returns>The summary page.</returns>
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var summary = await _accounts.GetAccountSummaryAsync(CurrentUserId);
            return summary is null ? NotFound() : View(summary);
        }

        /// <summary>
        /// The personal details form.
        /// </summary>
        /// <returns>The form, filled in.</returns>
        [HttpGet]
        public async Task<IActionResult> EditProfile()
        {
            var profile = await _accounts.GetProfileAsync(CurrentUserId);
            return profile is null ? NotFound() : View(profile);
        }

        /// <summary>
        /// Saves the personal details.
        /// </summary>
        /// <param name="model">The form.</param>
        /// <returns>The summary page, or the form with its errors.</returns>
        [HttpPost]
        public async Task<IActionResult> EditProfile(ProfileViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var result = await _accounts.UpdateProfileAsync(CurrentUserId, model);
            if (!result.Succeeded)
            {
                AddIdentityErrors(result.Errors);
                return View(model);
            }

            SetSuccess("Your details have been saved.");
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// The change-password form.
        /// </summary>
        /// <returns>The empty form.</returns>
        [HttpGet]
        public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

        /// <summary>
        /// Changes the password after checking the current one.
        /// </summary>
        /// <param name="model">The form.</param>
        /// <returns>The summary page, or the form with its errors.</returns>
        [HttpPost]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var result = await _accounts.ChangePasswordAsync(CurrentUserId, model);
            if (!result.Succeeded)
            {
                AddIdentityErrors(result.Errors, passwordField: nameof(ChangePasswordViewModel.NewPassword));
                return View(new ChangePasswordViewModel());
            }

            SetSuccess("Your password has been changed.");
            return RedirectToAction(nameof(Index));
        }
    }
}
