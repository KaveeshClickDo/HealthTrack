using HealthTrack.Models.ViewModels;
using HealthTrack.Security;
using HealthTrack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthTrack.Controllers
{
    /// <summary>
    /// Registration, login and logout (UC01–UC04). Everything except Logout is open to guests;
    /// a signed-in user who opens these pages goes to their dashboard.
    /// </summary>
    public class AccountController : BaseController
    {
        private readonly IAccountService _accounts;

        /// <summary>
        /// Creates the controller.
        /// </summary>
        /// <param name="accounts">The account service.</param>
        public AccountController(IAccountService accounts)
        {
            _accounts = accounts;
        }

        /// <summary>
        /// Lets the visitor choose between a client and a trainer account. Admins can never self-register.
        /// </summary>
        /// <returns>The choice page.</returns>
        [AllowAnonymous]
        [HttpGet]
        public IActionResult Register() => CurrentRole() is { } role ? RedirectToDashboard(role) : View();

        /// <summary>
        /// The client registration form (UC01).
        /// </summary>
        /// <returns>The empty form.</returns>
        [AllowAnonymous]
        [HttpGet]
        public IActionResult RegisterClient() =>
            CurrentRole() is { } role ? RedirectToDashboard(role) : View(new RegisterClientViewModel());

        /// <summary>
        /// Creates the client account and signs the new client in.
        /// </summary>
        /// <param name="model">The form.</param>
        /// <returns>The client dashboard, or the form with its errors.</returns>
        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> RegisterClient(RegisterClientViewModel model)
        {
            if (CurrentRole() is { } role)
            {
                return RedirectToDashboard(role);
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var result = await _accounts.RegisterClientAsync(model);
            if (!result.Succeeded)
            {
                AddIdentityErrors(result.Errors);
                return View(model);
            }

            await _accounts.SignInAsync(result.User);
            SetSuccess($"Welcome to HealthTrack, {result.User.FirstName}! Your account is ready.");
            return RedirectToDashboard(AppRoles.Client);
        }

        /// <summary>
        /// The trainer and health professional registration form (UC02).
        /// </summary>
        /// <returns>The empty form.</returns>
        [AllowAnonymous]
        [HttpGet]
        public IActionResult RegisterTrainer() =>
            CurrentRole() is { } role ? RedirectToDashboard(role) : View(new RegisterTrainerViewModel());

        /// <summary>
        /// Creates the trainer account, which waits for an administrator's approval (BR-03).
        /// </summary>
        /// <param name="model">The form.</param>
        /// <returns>The login page with the pending message, or the form with its errors.</returns>
        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> RegisterTrainer(RegisterTrainerViewModel model)
        {
            if (CurrentRole() is { } role)
            {
                return RedirectToDashboard(role);
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var result = await _accounts.RegisterTrainerAsync(model);
            if (!result.Succeeded)
            {
                AddIdentityErrors(result.Errors);
                return View(model);
            }

            SetSuccess("Thank you for registering. Your trainer account is waiting for an administrator's approval; " +
                "you can log in once it has been approved.");
            return RedirectToAction(nameof(Login));
        }

        /// <summary>
        /// The login form (UC03).
        /// </summary>
        /// <param name="returnUrl">The page to return to after signing in.</param>
        /// <returns>The form.</returns>
        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login(string? returnUrl = null) =>
            CurrentRole() is { } role ? RedirectToDashboard(role) : View(new LoginViewModel { ReturnUrl = returnUrl });

        /// <summary>
        /// Signs the user in, then returns to a local returnUrl or the role's dashboard.
        /// </summary>
        /// <param name="model">The form.</param>
        /// <returns>A redirect, or the form with the reason sign-in was refused.</returns>
        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (CurrentRole() is { } currentRole)
            {
                return RedirectToDashboard(currentRole);
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var result = await _accounts.LoginAsync(model.Email, model.Password);
            var error = result.Status switch
            {
                LoginStatus.Succeeded => null,
                LoginStatus.LockedOut => "Too many failed attempts. Try again in 15 minutes.",
                LoginStatus.PendingApproval => "Your trainer account is waiting for an administrator's approval.",
                LoginStatus.Inactive => "This account is not active. Please contact the administrator.",
                _ => "Invalid email or password."
            };

            if (error is not null)
            {
                ModelState.AddModelError(string.Empty, error);
                return View(model);
            }

            // Only local return URLs are followed, so the login page can't be used as an open redirect.
            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return LocalRedirect(model.ReturnUrl);
            }

            return RedirectToDashboard(result.Role);
        }

        /// <summary>
        /// Signs the user out (UC04). POST only, with the anti-forgery token, so a link can't sign anyone out.
        /// </summary>
        /// <returns>The home page.</returns>
        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await _accounts.LogoutAsync(CurrentUserId);
            SetSuccess("You have been logged out.");
            return RedirectToAction("Index", "Home");
        }
    }
}
