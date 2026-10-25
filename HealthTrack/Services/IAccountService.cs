using HealthTrack.Models.Entities;
using HealthTrack.Models.ViewModels;
using Microsoft.AspNetCore.Identity;

namespace HealthTrack.Services
{
    /// <summary>
    /// Registration, sign-in and the signed-in user's own account (UC01–UC05).
    /// </summary>
    public interface IAccountService
    {
        /// <summary>
        /// Creates an active Client account with its profile, first measurement and default reminder settings,
        /// all in one transaction (UC01).
        /// </summary>
        /// <param name="model">The validated registration form.</param>
        /// <returns>The new user, or Identity's errors.</returns>
        Task<RegistrationResult> RegisterClientAsync(RegisterClientViewModel model);

        /// <summary>
        /// Creates a Trainer account that waits for approval (BR-03), with its profile and default reminder settings,
        /// in one transaction, and tells every active administrator (UC02).
        /// </summary>
        /// <param name="model">The validated registration form.</param>
        /// <returns>The new user, or Identity's errors.</returns>
        Task<RegistrationResult> RegisterTrainerAsync(RegisterTrainerViewModel model);

        /// <summary>
        /// Checks the password with lockout, then the account status, then signs in (UC03). Every outcome is audited.
        /// </summary>
        /// <param name="email">The email address typed in.</param>
        /// <param name="password">The password typed in; never logged.</param>
        /// <returns>The outcome and, on success, the user's role.</returns>
        Task<LoginResult> LoginAsync(string email, string password);

        /// <summary>
        /// Signs a user in without a password check, for example straight after registration; records the login.
        /// </summary>
        /// <param name="user">A user who is allowed to sign in.</param>
        /// <returns>A task that completes when the cookie is issued.</returns>
        Task SignInAsync(ApplicationUser user);

        /// <summary>
        /// Signs the current user out and records it (UC04).
        /// </summary>
        /// <param name="userId">The user signing out.</param>
        /// <returns>A task that completes when the cookie is cleared.</returns>
        Task LogoutAsync(string userId);

        /// <summary>
        /// A read-only summary of a user's account.
        /// </summary>
        /// <param name="userId">The user.</param>
        /// <returns>The summary, or null when the user doesn't exist.</returns>
        Task<AccountSummaryViewModel?> GetAccountSummaryAsync(string userId);

        /// <summary>
        /// A user's editable personal details (UC05).
        /// </summary>
        /// <param name="userId">The user.</param>
        /// <returns>The details, or null when the user doesn't exist.</returns>
        Task<ProfileViewModel?> GetProfileAsync(string userId);

        /// <summary>
        /// Saves a user's personal details and records the change, without the values (UC05).
        /// </summary>
        /// <param name="userId">The user.</param>
        /// <param name="model">The validated form.</param>
        /// <returns>Identity's result.</returns>
        Task<IdentityResult> UpdateProfileAsync(string userId, ProfileViewModel model);

        /// <summary>
        /// Changes a user's password after checking the current one, refreshes their sign-in and records it (UC05).
        /// </summary>
        /// <param name="userId">The user.</param>
        /// <param name="model">The validated form.</param>
        /// <returns>Identity's result; a wrong current password gives the PasswordMismatch error.</returns>
        Task<IdentityResult> ChangePasswordAsync(string userId, ChangePasswordViewModel model);
    }
}
