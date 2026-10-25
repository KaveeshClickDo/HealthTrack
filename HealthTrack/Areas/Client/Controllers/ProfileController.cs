using HealthTrack.Models.ViewModels;
using HealthTrack.Services;
using Microsoft.AspNetCore.Mvc;

namespace HealthTrack.Areas.Client.Controllers
{
    /// <summary>
    /// The client's health profile (goal, activity, target weight, medical details and whether the trainer sees them) and
    /// their privacy rights (UC16, BR-22): download all their data, or delete the account.
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

        /// <summary>
        /// Downloads everything HealthTrack holds about the client as a JSON file (UC16); a POST, so the download needs the
        /// anti-forgery token and can't be triggered from another site.
        /// </summary>
        /// <returns>The JSON file.</returns>
        [HttpPost]
        public async Task<IActionResult> ExportMyData()
        {
            var export = await _accounts.ExportPersonalDataAsync(CurrentUserId);
            return export is null ? NotFound() : File(export.Content, "application/json", export.FileName);
        }

        /// <summary>
        /// Explains exactly what deleting the account removes and keeps, and asks for the password.
        /// </summary>
        /// <returns>The confirmation page.</returns>
        [HttpGet]
        public IActionResult DeleteAccount() => View(new DeleteAccountViewModel());

        /// <summary>
        /// Deletes the account after the password check (UC16, BR-22) and signs the user out.
        /// </summary>
        /// <param name="model">The form with the password.</param>
        /// <returns>The home page with a confirmation, or the form with its error.</returns>
        [HttpPost]
        public async Task<IActionResult> DeleteAccount(DeleteAccountViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            switch (await _accounts.DeleteAccountAsync(CurrentUserId, model.Password!))
            {
                case DeleteAccountStatus.NotFound:
                    return NotFound();
                case DeleteAccountStatus.WrongPassword:
                    ModelState.AddModelError(nameof(model.Password), "That password isn't right. Nothing has been deleted.");
                    return View(model);
                case DeleteAccountStatus.LockedOut:
                    ModelState.AddModelError(string.Empty, "Too many wrong passwords. Your account is locked for 15 minutes; nothing has been deleted.");
                    return View(model);
            }

            SetSuccess("Your account has been deleted and your health records removed. Thank you for using HealthTrack.");
            return RedirectToAction("Index", "Home", new { area = "" });
        }
    }
}
