using System.Globalization;
using HealthTrack.Data;
using HealthTrack.Helpers;
using HealthTrack.Models.Entities;
using HealthTrack.Models.Enums;
using HealthTrack.Models.ViewModels;
using HealthTrack.Security;
using Microsoft.AspNetCore.Identity;

namespace HealthTrack.Services
{
    /// <summary>
    /// Registration, sign-in and the signed-in user's own account, on top of ASP.NET Core Identity.
    /// </summary>
    public class AccountService : IAccountService
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IAuditService _audit;
        private readonly INotificationService _notifications;
        private readonly IClock _clock;

        /// <summary>
        /// Creates the service.
        /// </summary>
        /// <param name="db">The database context; UserManager uses the same scoped instance, so one transaction covers both.</param>
        /// <param name="userManager">Identity's user manager.</param>
        /// <param name="signInManager">Identity's sign-in manager.</param>
        /// <param name="audit">Records registrations, logins and changes.</param>
        /// <param name="notifications">Tells administrators about new trainers.</param>
        /// <param name="clock">Supplies the time.</param>
        public AccountService(ApplicationDbContext db, UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager, IAuditService audit, INotificationService notifications, IClock clock)
        {
            _db = db;
            _userManager = userManager;
            _signInManager = signInManager;
            _audit = audit;
            _notifications = notifications;
            _clock = clock;
        }

        /// <inheritdoc />
        public async Task<RegistrationResult> RegisterClientAsync(RegisterClientViewModel model)
        {
            var now = _clock.Now;

            // One transaction around the user, role, profile, first measurement, preferences and audit row,
            // so a failure never leaves a half-created account (spec 1.3). Returning early rolls it back.
            await using var transaction = await _db.Database.BeginTransactionAsync();

            var user = NewUser(model, AccountStatus.Active, now);
            var created = await CreateUserInRoleAsync(user, model.Password, AppRoles.Client);
            if (!created.Succeeded)
            {
                return RegistrationResult.Failed(created.Errors);
            }

            var profile = new ClientProfile
            {
                UserId = user.Id,
                FitnessGoal = model.FitnessGoal!.Value,
                ActivityLevel = model.ActivityLevel!.Value,
                TargetWeightKg = model.TargetWeightKg,
                MedicalConditions = model.MedicalConditions.NullIfBlank(),
                Allergies = model.Allergies.NullIfBlank(),
                CurrentMedications = model.CurrentMedications.NullIfBlank(),
                EmergencyContactName = model.EmergencyContactName.NullIfBlank(),
                EmergencyContactPhone = model.EmergencyContactPhone.NullIfBlank(),
                CreatedAt = now
            };

            // The height and weight from the form become the client's first measurement (spec 1.3).
            profile.Measurements.Add(new HealthMeasurement
            {
                MeasuredAt = now,
                WeightKg = model.WeightKg!.Value,
                HeightCm = model.HeightCm!.Value
            });

            _db.ClientProfiles.Add(profile);
            _db.NotificationPreferences.Add(NotificationPreference.CreateDefault(user.Id));
            await _db.SaveChangesAsync();

            await _audit.LogAsync(user.Id, AuditAction.Register, nameof(ClientProfile),
                profile.ClientId.ToString(CultureInfo.InvariantCulture), "Client account registered.");

            await transaction.CommitAsync();
            return RegistrationResult.Success(user);
        }

        /// <inheritdoc />
        public async Task<RegistrationResult> RegisterTrainerAsync(RegisterTrainerViewModel model)
        {
            var now = _clock.Now;
            await using var transaction = await _db.Database.BeginTransactionAsync();

            // BR-03: trainers can't sign in until an administrator approves them.
            var user = NewUser(model, AccountStatus.PendingApproval, now);
            var created = await CreateUserInRoleAsync(user, model.Password, AppRoles.Trainer);
            if (!created.Succeeded)
            {
                return RegistrationResult.Failed(created.Errors);
            }

            var professionalType = model.ProfessionalType!.Value;
            var profile = new TrainerProfile
            {
                UserId = user.Id,
                ProfessionalType = professionalType,
                Specialization = model.Specialization.Trim(),
                Qualifications = model.Qualifications.Trim(),
                LicenseNumber = model.LicenseNumber.NullIfBlank(),
                YearsOfExperience = model.YearsOfExperience!.Value,
                Bio = model.Bio.NullIfBlank(),
                MaxClients = 20,
                ApprovedAt = null,
                CreatedAt = now
            };

            _db.TrainerProfiles.Add(profile);
            _db.NotificationPreferences.Add(NotificationPreference.CreateDefault(user.Id));
            await _db.SaveChangesAsync();

            await _audit.LogAsync(user.Id, AuditAction.Register, nameof(TrainerProfile),
                profile.TrainerId.ToString(CultureInfo.InvariantCulture), "Trainer account registered; waiting for approval.");

            // Every active administrator hears about the request, so it can be approved (UC30).
            var message = $"{user.FullName} registered as a {professionalType.GetDisplayName().ToLowerInvariant()} and is waiting for approval.";
            foreach (var admin in await _userManager.GetUsersInRoleAsync(AppRoles.Admin))
            {
                if (admin.AccountStatus == AccountStatus.Active)
                {
                    await _notifications.NotifyAsync(admin.Id, NotificationType.AccountUpdate,
                        "New trainer registration", message, "/Admin/TrainerApprovals");
                }
            }

            await transaction.CommitAsync();
            return RegistrationResult.Success(user);
        }

        /// <inheritdoc />
        public async Task<LoginResult> LoginAsync(string email, string password)
        {
            var user = await _userManager.FindByEmailAsync(email.Trim());
            if (user is null)
            {
                // Same answer as a wrong password, so the page doesn't reveal which emails have accounts.
                // The typed email isn't stored: people sometimes type their password into that box.
                await _audit.LogAsync(null, AuditAction.LoginFailed, details: "Unknown email address.");
                return LoginResult.Failed(LoginStatus.InvalidCredentials);
            }

            var wasLockedOut = await _userManager.IsLockedOutAsync(user);
            var check = await _signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);

            if (check.IsLockedOut)
            {
                if (wasLockedOut)
                {
                    await _audit.LogAsync(user.Id, AuditAction.LoginFailed, details: "Sign-in attempt while locked out.");
                }
                else
                {
                    // This failed attempt was the one that locked the account.
                    var lockout = _userManager.Options.Lockout;
                    await _audit.LogAsync(user.Id, AuditAction.LoginFailed, details: "Wrong password.");
                    await _audit.LogAsync(user.Id, AuditAction.AccountLocked,
                        details: $"Locked for {lockout.DefaultLockoutTimeSpan.TotalMinutes:0} minutes after {lockout.MaxFailedAccessAttempts} failed attempts.");
                }

                return LoginResult.Failed(LoginStatus.LockedOut);
            }

            if (!check.Succeeded)
            {
                await _audit.LogAsync(user.Id, AuditAction.LoginFailed, details: "Wrong password.");
                return LoginResult.Failed(LoginStatus.InvalidCredentials);
            }

            // The status is checked only after the password is right, so it can't be learned without the password.
            if (!user.CanSignIn())
            {
                await _audit.LogAsync(user.Id, AuditAction.LoginFailed,
                    details: $"Correct password, but the account is {user.AccountStatus.GetDisplayName().ToLowerInvariant()}.");
                return LoginResult.Failed(user.AccountStatus == AccountStatus.PendingApproval
                    ? LoginStatus.PendingApproval
                    : LoginStatus.Inactive);
            }

            return LoginResult.Success(await SignInCoreAsync(user));
        }

        /// <inheritdoc />
        public async Task SignInAsync(ApplicationUser user) => await SignInCoreAsync(user);

        /// <inheritdoc />
        public async Task LogoutAsync(string userId)
        {
            await _signInManager.SignOutAsync();
            await _audit.LogAsync(userId, AuditAction.Logout);
        }

        /// <inheritdoc />
        public async Task<AccountSummaryViewModel?> GetAccountSummaryAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user is null)
            {
                return null;
            }

            return new AccountSummaryViewModel
            {
                FirstName = user.FirstName,
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                PhoneNumber = user.PhoneNumber,
                DateOfBirth = user.DateOfBirth,
                Age = HealthCalculator.CalculateAge(user.DateOfBirth, _clock.Today),
                Gender = user.Gender,
                Role = (await _userManager.GetRolesAsync(user)).FirstOrDefault() ?? string.Empty,
                AccountStatus = user.AccountStatus,
                CreatedAt = user.CreatedAt,
                LastLoginAt = user.LastLoginAt
            };
        }

        /// <inheritdoc />
        public async Task<ProfileViewModel?> GetProfileAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            return user is null
                ? null
                : new ProfileViewModel
                {
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    PhoneNumber = user.PhoneNumber,
                    DateOfBirth = user.DateOfBirth,
                    Gender = user.Gender
                };
        }

        /// <inheritdoc />
        public async Task<IdentityResult> UpdateProfileAsync(string userId, ProfileViewModel model)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user is null)
            {
                return UserNotFound();
            }

            user.FirstName = model.FirstName.Trim();
            user.LastName = model.LastName.Trim();
            user.PhoneNumber = model.PhoneNumber.NullIfBlank();
            user.DateOfBirth = model.DateOfBirth!.Value.Date;
            user.Gender = model.Gender!.Value;

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                await _audit.LogAsync(user.Id, AuditAction.Update, nameof(ApplicationUser), user.Id, "Personal details updated.");
            }

            return result;
        }

        /// <inheritdoc />
        public async Task<IdentityResult> ChangePasswordAsync(string userId, ChangePasswordViewModel model)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user is null)
            {
                return UserNotFound();
            }

            var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
            if (result.Succeeded)
            {
                // Changing the password changes the security stamp; refresh the cookie so this session stays signed in.
                await _signInManager.RefreshSignInAsync(user);
                await _audit.LogAsync(user.Id, AuditAction.Update, nameof(ApplicationUser), user.Id, "Password changed.");
            }

            return result;
        }

        private static ApplicationUser NewUser(RegisterViewModel model, AccountStatus status, DateTime now)
        {
            var email = model.Email.Trim();
            return new ApplicationUser
            {
                // BR-01: the email address is also the user name.
                UserName = email,
                Email = email,
                FirstName = model.FirstName.Trim(),
                LastName = model.LastName.Trim(),
                DateOfBirth = model.DateOfBirth!.Value.Date,
                Gender = model.Gender!.Value,
                PhoneNumber = model.PhoneNumber.NullIfBlank(),
                AccountStatus = status,
                PrivacyConsentAt = now,
                CreatedAt = now
            };
        }

        // BR-05: every user gets exactly one role.
        private async Task<IdentityResult> CreateUserInRoleAsync(ApplicationUser user, string password, string role)
        {
            var result = await _userManager.CreateAsync(user, password);
            return result.Succeeded ? await _userManager.AddToRoleAsync(user, role) : result;
        }

        private async Task<string?> SignInCoreAsync(ApplicationUser user)
        {
            user.LastLoginAt = _clock.Now;
            await _userManager.UpdateAsync(user);
            await _signInManager.SignInAsync(user, isPersistent: false);
            await _audit.LogAsync(user.Id, AuditAction.Login);
            return (await _userManager.GetRolesAsync(user)).FirstOrDefault();
        }

        private static IdentityResult UserNotFound() =>
            IdentityResult.Failed(new IdentityError { Code = "UserNotFound", Description = "Your account could not be found." });
    }
}
