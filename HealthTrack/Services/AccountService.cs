using System.Globalization;
using HealthTrack.Data;
using HealthTrack.Helpers;
using HealthTrack.Models.Entities;
using HealthTrack.Models.Enums;
using HealthTrack.Models.ViewModels;
using HealthTrack.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

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

        /// <inheritdoc />
        public async Task<RegistrationResult> CreateAdminAsync(CreateAdminViewModel model, string actingAdminId)
        {
            var now = _clock.Now;
            await using var transaction = await _db.Database.BeginTransactionAsync();

            var user = NewUser(model, AccountStatus.Active, now);
            var created = await CreateUserInRoleAsync(user, model.Password, AppRoles.Admin);
            if (!created.Succeeded)
            {
                return RegistrationResult.Failed(created.Errors);
            }

            _db.NotificationPreferences.Add(NotificationPreference.CreateDefault(user.Id));
            await _db.SaveChangesAsync();
            await _audit.LogAsync(actingAdminId, AuditAction.Create, nameof(ApplicationUser), user.Id,
                $"Administrator account created for {user.Email}.");

            await transaction.CommitAsync();
            return RegistrationResult.Success(user);
        }

        /// <inheritdoc />
        public async Task<bool> ApproveTrainerAsync(int trainerId, string actingAdminId)
        {
            var trainer = await _db.TrainerProfiles.Include(t => t.User).FirstOrDefaultAsync(t => t.TrainerId == trainerId);
            if (trainer is null)
            {
                return false;
            }

            EnsurePendingApproval(trainer);

            await using var transaction = await _db.Database.BeginTransactionAsync();

            // BR-03: approval is what lets a trainer sign in.
            trainer.ApprovedAt = _clock.Now;
            trainer.User.AccountStatus = AccountStatus.Active;
            ThrowIfFailed(await _userManager.UpdateAsync(trainer.User));

            await _notifications.NotifyAsync(trainer.UserId, NotificationType.AccountUpdate,
                "Your trainer account has been approved",
                "Welcome to HealthTrack! You can now set your availability and start coaching your clients.",
                "/Trainer/Dashboard");
            await _audit.LogAsync(actingAdminId, AuditAction.Approve, nameof(TrainerProfile),
                trainer.TrainerId.ToString(CultureInfo.InvariantCulture), $"Trainer approved: {trainer.User.FullName}.");

            await transaction.CommitAsync();
            return true;
        }

        /// <inheritdoc />
        public async Task<bool> RejectTrainerAsync(int trainerId, string reason, string actingAdminId)
        {
            var trainer = await _db.TrainerProfiles.Include(t => t.User).FirstOrDefaultAsync(t => t.TrainerId == trainerId);
            if (trainer is null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new BusinessRuleException("BR-03", "Give a reason for the rejection.");
            }

            EnsurePendingApproval(trainer);

            await using var transaction = await _db.Database.BeginTransactionAsync();

            // A rejected registration can never sign in; the account is kept (deactivated) so the audit trail stays complete.
            trainer.User.AccountStatus = AccountStatus.Deactivated;
            ThrowIfFailed(await _userManager.UpdateSecurityStampAsync(trainer.User));

            await _audit.LogAsync(actingAdminId, AuditAction.Reject, nameof(TrainerProfile),
                trainer.TrainerId.ToString(CultureInfo.InvariantCulture),
                $"Trainer registration rejected ({trainer.User.FullName}): {reason.Trim()}");

            await transaction.CommitAsync();
            return true;
        }

        /// <inheritdoc />
        public async Task<bool> SetAccountStatusAsync(string userId, AccountStatus status, string actingAdminId)
        {
            if (status is not (AccountStatus.Suspended or AccountStatus.Active))
            {
                throw new ArgumentOutOfRangeException(nameof(status), status, "Accounts can only be suspended or reactivated here.");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user is null)
            {
                return false;
            }

            if (status == AccountStatus.Suspended)
            {
                // BR-04: no admin can suspend themselves or the last active admin.
                if (userId == actingAdminId)
                {
                    throw new BusinessRuleException("BR-04", "You can't suspend your own account.");
                }

                if (user.AccountStatus != AccountStatus.Active)
                {
                    throw new BusinessRuleException("BR-05", "Only an active account can be suspended.");
                }

                if (await _userManager.IsInRoleAsync(user, AppRoles.Admin))
                {
                    var activeAdmins = (await _userManager.GetUsersInRoleAsync(AppRoles.Admin))
                        .Count(a => a.AccountStatus == AccountStatus.Active);
                    if (activeAdmins <= 1)
                    {
                        throw new BusinessRuleException("BR-04", "This is the last active administrator, so it can't be suspended.");
                    }
                }
            }
            else if (user.AccountStatus != AccountStatus.Suspended)
            {
                // Pending trainers are approved, and deactivated accounts stay closed.
                throw new BusinessRuleException("BR-05", "Only a suspended account can be reactivated.");
            }

            await using var transaction = await _db.Database.BeginTransactionAsync();

            // BR-05: a new security stamp makes the security-stamp validator sign the user out within a minute.
            user.AccountStatus = status;
            ThrowIfFailed(await _userManager.UpdateSecurityStampAsync(user));

            if (status == AccountStatus.Active)
            {
                await _notifications.NotifyAsync(user.Id, NotificationType.AccountUpdate, "Your account has been reactivated",
                    "Welcome back! You can use HealthTrack again.");
            }

            await _audit.LogAsync(actingAdminId, AuditAction.Update, nameof(ApplicationUser), user.Id,
                status == AccountStatus.Suspended ? $"Account suspended: {user.Email}." : $"Account reactivated: {user.Email}.");

            await transaction.CommitAsync();
            return true;
        }

        /// <inheritdoc />
        public async Task<PagedResult<UserListItemViewModel>> GetUsersAsync(UserFilterViewModel filter, int page)
        {
            var query =
                from user in _db.Users.AsNoTracking()
                join userRole in _db.UserRoles on user.Id equals userRole.UserId
                join role in _db.Roles on userRole.RoleId equals role.Id
                select new { User = user, Role = role.Name };

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                // Upper-case on both sides, so the search ignores case on every provider.
                var term = filter.Search.Trim().ToUpperInvariant();
                query = query.Where(x => (x.User.NormalizedEmail != null && x.User.NormalizedEmail.Contains(term))
                    || (x.User.FirstName + " " + x.User.LastName).ToUpper().Contains(term));
            }

            if (!string.IsNullOrEmpty(filter.Role))
            {
                query = query.Where(x => x.Role == filter.Role);
            }

            if (filter.Status is { } status)
            {
                query = query.Where(x => x.User.AccountStatus == status);
            }

            return await query
                .OrderBy(x => x.User.FirstName)
                .ThenBy(x => x.User.LastName)
                .Select(x => new UserListItemViewModel
                {
                    UserId = x.User.Id,
                    FullName = x.User.FirstName + " " + x.User.LastName,
                    Email = x.User.Email ?? string.Empty,
                    Role = x.Role ?? string.Empty,
                    AccountStatus = x.User.AccountStatus,
                    CreatedAt = x.User.CreatedAt,
                    LastLoginAt = x.User.LastLoginAt
                })
                .ToPagedResultAsync(page);
        }

        /// <inheritdoc />
        public async Task<UserDetailsViewModel?> GetUserDetailsAsync(string userId, string currentUserId)
        {
            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
            if (user is null)
            {
                return null;
            }

            // Projections pick counts and labels only, so encrypted medical fields and measurement values are never read.
            var client = await _db.ClientProfiles.AsNoTracking()
                .Where(c => c.UserId == userId)
                .Select(c => new ClientActivityViewModel
                {
                    ClientId = c.ClientId,
                    FitnessGoal = c.FitnessGoal,
                    ActivityLevel = c.ActivityLevel,
                    TrainerName = c.Assignments.Where(a => a.EndDate == null)
                        .Select(a => a.Trainer.User.FirstName + " " + a.Trainer.User.LastName).FirstOrDefault(),
                    WorkoutsLogged = c.WorkoutLogs.Count,
                    MealsLogged = c.MealLogs.Count,
                    MeasurementsRecorded = c.Measurements.Count,
                    Appointments = c.Appointments.Count,
                    LastWorkoutDate = c.WorkoutLogs.Max(w => (DateTime?)w.WorkoutDate)
                })
                .FirstOrDefaultAsync();

            var trainer = await _db.TrainerProfiles.AsNoTracking()
                .Where(t => t.UserId == userId)
                .Select(t => new TrainerActivityViewModel
                {
                    TrainerId = t.TrainerId,
                    ProfessionalType = t.ProfessionalType,
                    Specialization = t.Specialization,
                    LicenseNumber = t.LicenseNumber,
                    YearsOfExperience = t.YearsOfExperience,
                    ApprovedAt = t.ApprovedAt,
                    ActiveClients = t.Assignments.Count(a => a.EndDate == null),
                    MaxClients = t.MaxClients,
                    WorkoutPlans = t.WorkoutPlans.Count,
                    DietPlans = t.DietPlans.Count,
                    Appointments = t.Appointments.Count
                })
                .FirstOrDefaultAsync();

            var recentEvents = await _db.AuditLogs.AsNoTracking()
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.Timestamp)
                .ThenByDescending(a => a.AuditLogId)
                .Take(10)
                .Select(a => new AuditLogItemViewModel
                {
                    AuditLogId = a.AuditLogId,
                    Timestamp = a.Timestamp,
                    UserEmail = user.Email,
                    Action = a.Action,
                    EntityName = a.EntityName,
                    EntityId = a.EntityId,
                    Details = a.Details,
                    IpAddress = a.IpAddress
                })
                .ToListAsync();

            return new UserDetailsViewModel
            {
                UserId = user.Id,
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                PhoneNumber = user.PhoneNumber,
                DateOfBirth = user.DateOfBirth,
                Age = HealthCalculator.CalculateAge(user.DateOfBirth, _clock.Today),
                Gender = user.Gender,
                Role = (await _userManager.GetRolesAsync(user)).FirstOrDefault() ?? string.Empty,
                AccountStatus = user.AccountStatus,
                CreatedAt = user.CreatedAt,
                LastLoginAt = user.LastLoginAt,
                LockoutEnd = user.LockoutEnd > DateTimeOffset.UtcNow ? user.LockoutEnd : null,
                IsCurrentUser = user.Id == currentUserId,
                Client = client,
                Trainer = trainer,
                RecentEvents = recentEvents
            };
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<PendingTrainerViewModel>> GetPendingTrainersAsync() =>
            await _db.TrainerProfiles.AsNoTracking()
                .Where(t => t.User.AccountStatus == AccountStatus.PendingApproval)
                .OrderBy(t => t.CreatedAt)
                .Select(t => new PendingTrainerViewModel
                {
                    TrainerId = t.TrainerId,
                    FullName = t.User.FirstName + " " + t.User.LastName,
                    Email = t.User.Email ?? string.Empty,
                    PhoneNumber = t.User.PhoneNumber,
                    ProfessionalType = t.ProfessionalType,
                    Specialization = t.Specialization,
                    Qualifications = t.Qualifications,
                    LicenseNumber = t.LicenseNumber,
                    YearsOfExperience = t.YearsOfExperience,
                    Bio = t.Bio,
                    RegisteredAt = t.CreatedAt
                })
                .ToListAsync();

        // BR-03: only a registration that is still waiting can be approved or rejected.
        private static void EnsurePendingApproval(TrainerProfile trainer)
        {
            if (trainer.User.AccountStatus != AccountStatus.PendingApproval)
            {
                throw new BusinessRuleException("BR-03", "This trainer registration has already been dealt with.");
            }
        }

        // Identity failures here (for example a concurrency clash) aren't the user's fault; let the global handler show them.
        private static void ThrowIfFailed(IdentityResult result)
        {
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(string.Join(" ", result.Errors.Select(e => e.Description)));
            }
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
