using System.Security.Claims;
using HealthTrack.Models.Enums;
using HealthTrack.Security;
using HealthTrack.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace HealthTrack.Controllers
{
    /// <summary>
    /// The base of every controller: the signed-in user, auditing, alert messages and the role's dashboard.
    /// </summary>
    public abstract class BaseController : Controller
    {
        /// <summary>
        /// The signed-in user's ID (the NameIdentifier claim). "My" data is always resolved from this, never from a form or URL.
        /// </summary>
        /// <exception cref="InvalidOperationException">Nobody is signed in.</exception>
        protected string CurrentUserId =>
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("No user is signed in.");

        /// <summary>
        /// Records an event for the signed-in user (or for nobody, when signed out).
        /// </summary>
        /// <param name="action">What happened.</param>
        /// <param name="entityName">The kind of record affected.</param>
        /// <param name="entityId">The key of the affected record.</param>
        /// <param name="details">A short summary; never passwords or health values.</param>
        /// <returns>A task that completes when the row is saved.</returns>
        protected Task AuditAsync(AuditAction action, string? entityName = null, string? entityId = null, string? details = null)
        {
            // Resolved per call, so controllers that never audit don't need the dependency in their constructor.
            var audit = HttpContext.RequestServices.GetRequiredService<IAuditService>();
            return audit.LogAsync(User.FindFirstValue(ClaimTypes.NameIdentifier), action, entityName, entityId, details);
        }

        /// <summary>
        /// Shows a success message on the next page (through the _Alerts partial).
        /// </summary>
        /// <param name="message">The message.</param>
        protected void SetSuccess(string message) => TempData["Success"] = message;

        /// <summary>
        /// Shows an error message on the next page (through the _Alerts partial).
        /// </summary>
        /// <param name="message">The message.</param>
        protected void SetError(string message) => TempData["Error"] = message;

        /// <summary>
        /// The signed-in user's role, or null when signed out (or without a known role).
        /// </summary>
        /// <returns>Admin, Trainer, Client or null.</returns>
        protected string? CurrentRole() => AppRoles.All.FirstOrDefault(User.IsInRole);

        /// <summary>
        /// Redirects to a role's dashboard: /Admin/Dashboard, /Trainer/Dashboard or /Client/Dashboard (spec 1.3).
        /// </summary>
        /// <param name="role">The role; the signed-in user's role when omitted.</param>
        /// <returns>The redirect; the home page for an unknown role.</returns>
        protected IActionResult RedirectToDashboard(string? role = null) => (role ?? CurrentRole()) switch
        {
            AppRoles.Admin => RedirectToAction("Index", "Dashboard", new { area = "Admin" }),
            AppRoles.Trainer => RedirectToAction("Index", "Dashboard", new { area = "Trainer" }),
            AppRoles.Client => RedirectToAction("Index", "Dashboard", new { area = "Client" }),
            _ => RedirectToAction("Index", "Home", new { area = "" })
        };

        /// <summary>
        /// Puts Identity's errors next to the matching form fields. The user name is the email address, so user-name
        /// errors go to the Email field, and a duplicate user name isn't repeated when a duplicate email is reported.
        /// </summary>
        /// <param name="errors">Identity's errors.</param>
        /// <param name="passwordField">The form field for password-rule errors.</param>
        protected void AddIdentityErrors(IEnumerable<IdentityError> errors, string passwordField = "Password")
        {
            var errorList = errors.ToList();
            var hasDuplicateEmail = errorList.Any(e => e.Code == nameof(IdentityErrorDescriber.DuplicateEmail));

            foreach (var error in errorList)
            {
                if (hasDuplicateEmail && error.Code == nameof(IdentityErrorDescriber.DuplicateUserName))
                {
                    continue;
                }

                var field = error.Code switch
                {
                    nameof(IdentityErrorDescriber.PasswordMismatch) => "CurrentPassword",
                    _ when error.Code.StartsWith("Password", StringComparison.Ordinal) => passwordField,
                    _ when error.Code.Contains("Email", StringComparison.Ordinal) || error.Code.Contains("UserName", StringComparison.Ordinal) => "Email",
                    _ => string.Empty
                };

                ModelState.AddModelError(field, error.Description);
            }
        }
    }
}
