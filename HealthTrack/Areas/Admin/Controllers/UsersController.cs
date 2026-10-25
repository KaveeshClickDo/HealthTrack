using HealthTrack.Helpers;
using HealthTrack.Models.Enums;
using HealthTrack.Models.ViewModels;
using HealthTrack.Services;
using Microsoft.AspNetCore.Mvc;

namespace HealthTrack.Areas.Admin.Controllers
{
    /// <summary>
    /// Manage users (UC29): search, details, create administrators, suspend and reactivate accounts.
    /// </summary>
    public class UsersController : AdminAreaController
    {
        private readonly IAccountService _accounts;

        /// <summary>
        /// Creates the controller.
        /// </summary>
        /// <param name="accounts">The account service.</param>
        public UsersController(IAccountService accounts)
        {
            _accounts = accounts;
        }

        /// <summary>
        /// The user list, 20 per page, searchable by name or email and filtered by role and status.
        /// </summary>
        /// <param name="filter">The filters from the query string.</param>
        /// <param name="page">The page number.</param>
        /// <returns>The list.</returns>
        [HttpGet]
        public async Task<IActionResult> Index(UserFilterViewModel filter, int page = 1) =>
            View(new UserListViewModel { Filter = filter, Results = await _accounts.GetUsersAsync(filter, page) });

        /// <summary>
        /// One account's details and activity totals; never clinical notes or measurement values.
        /// </summary>
        /// <param name="id">The user's ID.</param>
        /// <returns>The details page, or 404.</returns>
        [HttpGet]
        public async Task<IActionResult> Details(string id)
        {
            var details = await _accounts.GetUserDetailsAsync(id, CurrentUserId);
            return details is null ? NotFound() : View(details);
        }

        /// <summary>
        /// The form for a new administrator.
        /// </summary>
        /// <returns>The empty form.</returns>
        [HttpGet]
        public IActionResult CreateAdmin() => View(new CreateAdminViewModel());

        /// <summary>
        /// Creates the administrator.
        /// </summary>
        /// <param name="model">The form.</param>
        /// <returns>The new account's details, or the form with its errors.</returns>
        [HttpPost]
        public async Task<IActionResult> CreateAdmin(CreateAdminViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var result = await _accounts.CreateAdminAsync(model, CurrentUserId);
            if (!result.Succeeded)
            {
                AddIdentityErrors(result.Errors);
                return View(model);
            }

            SetSuccess($"Administrator account created for {result.User.Email}. Share the password with them securely.");
            return RedirectToAction(nameof(Details), new { id = result.User.Id });
        }

        /// <summary>
        /// Suspends an account; the user is signed out within a minute (BR-05).
        /// </summary>
        /// <param name="id">The user's ID.</param>
        /// <returns>The details page.</returns>
        [HttpPost]
        public Task<IActionResult> Suspend(string id) =>
            ChangeStatusAsync(id, AccountStatus.Suspended, "The account has been suspended. The user is signed out within a minute.");

        /// <summary>
        /// Reactivates a suspended account.
        /// </summary>
        /// <param name="id">The user's ID.</param>
        /// <returns>The details page.</returns>
        [HttpPost]
        public Task<IActionResult> Reactivate(string id) =>
            ChangeStatusAsync(id, AccountStatus.Active, "The account has been reactivated.");

        private async Task<IActionResult> ChangeStatusAsync(string id, AccountStatus status, string successMessage)
        {
            try
            {
                if (!await _accounts.SetAccountStatusAsync(id, status, CurrentUserId))
                {
                    return NotFound();
                }

                SetSuccess(successMessage);
            }
            catch (BusinessRuleException ex)
            {
                SetError(ex.Message);
            }

            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
