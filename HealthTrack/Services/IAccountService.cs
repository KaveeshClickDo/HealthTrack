using HealthTrack.Models.Entities;
using HealthTrack.Models.Enums;
using HealthTrack.Models.ViewModels;
using Microsoft.AspNetCore.Identity;

namespace HealthTrack.Services
{
    /// <summary>
    /// Registration, sign-in and the signed-in user's own account (UC01–UC05); creating admins, trainer approvals and
    /// account status for administrators (UC29, UC30).
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

        /// <summary>
        /// Creates an active administrator (BR-04: admins are seeded or created by an admin, never self-registered).
        /// </summary>
        /// <param name="model">The validated form.</param>
        /// <param name="actingAdminId">The administrator creating the account.</param>
        /// <returns>The new user, or Identity's errors.</returns>
        Task<RegistrationResult> CreateAdminAsync(CreateAdminViewModel model, string actingAdminId);

        /// <summary>
        /// Approves a pending trainer: ApprovedAt is set, the account becomes Active and the trainer is told (UC30, BR-03).
        /// </summary>
        /// <param name="trainerId">The trainer profile.</param>
        /// <param name="actingAdminId">The administrator approving.</param>
        /// <returns>False when the trainer doesn't exist.</returns>
        /// <exception cref="Helpers.BusinessRuleException">The trainer isn't waiting for approval (BR-03).</exception>
        Task<bool> ApproveTrainerAsync(int trainerId, string actingAdminId);

        /// <summary>
        /// Rejects a pending trainer: the account is deactivated and the reason is kept in the audit log (UC30).
        /// </summary>
        /// <param name="trainerId">The trainer profile.</param>
        /// <param name="reason">Why the registration is rejected; required.</param>
        /// <param name="actingAdminId">The administrator rejecting.</param>
        /// <returns>False when the trainer doesn't exist.</returns>
        /// <exception cref="Helpers.BusinessRuleException">No reason, or the trainer isn't waiting for approval (BR-03).</exception>
        Task<bool> RejectTrainerAsync(int trainerId, string reason, string actingAdminId);

        /// <summary>
        /// Suspends an active account or reactivates a suspended one (UC29). The security stamp always changes, so a
        /// suspended user is signed out within a minute (BR-05).
        /// </summary>
        /// <param name="userId">The account.</param>
        /// <param name="status">Suspended or Active.</param>
        /// <param name="actingAdminId">The administrator making the change.</param>
        /// <returns>False when the user doesn't exist.</returns>
        /// <exception cref="Helpers.BusinessRuleException">
        /// BR-04: suspending yourself or the last active admin; BR-05: the account isn't in the right state.
        /// </exception>
        Task<bool> SetAccountStatusAsync(string userId, AccountStatus status, string actingAdminId);

        /// <summary>
        /// One page of users with their roles, by name (UC29).
        /// </summary>
        /// <param name="filter">Optional search, role and status filters.</param>
        /// <param name="page">The 1-based page.</param>
        /// <returns>The page.</returns>
        Task<PagedResult<UserListItemViewModel>> GetUsersAsync(UserFilterViewModel filter, int page);

        /// <summary>
        /// One account's details, activity totals and recent audit events, for an administrator.
        /// Never includes clinical notes or measurement values (spec 1.2).
        /// </summary>
        /// <param name="userId">The account.</param>
        /// <param name="currentUserId">The administrator looking, to flag their own account.</param>
        /// <returns>The details, or null when the user doesn't exist.</returns>
        Task<UserDetailsViewModel?> GetUserDetailsAsync(string userId, string currentUserId);

        /// <summary>
        /// Trainer registrations waiting for approval, oldest first (UC30).
        /// </summary>
        /// <returns>The pending trainers.</returns>
        Task<IReadOnlyList<PendingTrainerViewModel>> GetPendingTrainersAsync();

        /// <summary>
        /// The client profile of a user, so "my" data is always found from the signed-in user.
        /// </summary>
        /// <param name="userId">The user.</param>
        /// <returns>The ClientId, or null when the user has no client profile.</returns>
        Task<int?> GetClientIdAsync(string userId);

        /// <summary>
        /// The trainer profile of a user.
        /// </summary>
        /// <param name="userId">The user.</param>
        /// <returns>The TrainerId, or null when the user has no trainer profile.</returns>
        Task<int?> GetTrainerIdAsync(string userId);

        /// <summary>
        /// The signed-in client's health profile, with the medical details decrypted for them.
        /// </summary>
        /// <param name="userId">The client's user ID.</param>
        /// <returns>The form, or null when the user has no client profile.</returns>
        Task<HealthProfileViewModel?> GetHealthProfileAsync(string userId);

        /// <summary>
        /// Saves the signed-in client's health profile; the medical details are encrypted by the value converter (BR-20).
        /// </summary>
        /// <param name="userId">The client's user ID.</param>
        /// <param name="model">The form.</param>
        /// <returns>False when the user has no client profile.</returns>
        Task<bool> UpdateHealthProfileAsync(string userId, HealthProfileViewModel model);

        /// <summary>
        /// Everything the app holds about the signed-in client as a JSON file (UC16, BR-22): account, health profile with the
        /// medical details decrypted, settings, assignments, measurements, plans, logs, appointments, feedback and
        /// notifications. The export is audited.
        /// </summary>
        /// <param name="userId">The client's user ID.</param>
        /// <returns>The file, or null when the user has no client profile.</returns>
        Task<PersonalDataExport?> ExportPersonalDataAsync(string userId);

        /// <summary>
        /// Deletes the signed-in client's account (UC16, BR-22) after checking the password (with lockout): in one
        /// transaction the health records are deleted in dependency order and the user row is anonymised and locked for
        /// good; the audit log is kept, the deletion is audited and the user is signed out.
        /// </summary>
        /// <param name="userId">The client's user ID.</param>
        /// <param name="password">The current password.</param>
        /// <returns>What happened; nothing changes unless it is <see cref="DeleteAccountStatus.Deleted"/>.</returns>
        Task<DeleteAccountStatus> DeleteAccountAsync(string userId, string password);
    }
}
