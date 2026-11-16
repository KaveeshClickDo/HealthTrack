using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
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
        // The personal data export: readable JSON with enum names rather than numbers. The file is only ever downloaded, never
        // put into a page, so characters such as + and é can stay as they are instead of HTML-safe \u escapes.
        private static readonly JsonSerializerOptions ExportJson = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            Converters = { new JsonStringEnumConverter() }
        };

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

            if (client is not null)
            {
                client.Plans = await ClientPlansAsync(client.ClientId);
            }

            if (trainer is not null)
            {
                // What closing the account would touch (BR-23): clients still assigned, and open sessions to come.
                trainer.ActiveClientNames = await _db.TrainerAssignments.AsNoTracking()
                    .Where(a => a.TrainerId == trainer.TrainerId && a.EndDate == null)
                    .OrderBy(a => a.Client.User.FirstName)
                    .Select(a => a.Client.User.FirstName + " " + a.Client.User.LastName)
                    .ToListAsync();
                var now = _clock.Now;
                trainer.UpcomingAppointments = await _db.Appointments.AsNoTracking()
                    .Where(a => a.TrainerId == trainer.TrainerId && a.StartDateTime > now
                        && (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Approved || a.Status == AppointmentStatus.Rescheduled))
                    .OrderBy(a => a.StartDateTime)
                    .Select(a => new AppointmentListItemViewModel
                    {
                        AppointmentId = a.AppointmentId,
                        StartDateTime = a.StartDateTime,
                        DurationMinutes = a.DurationMinutes,
                        AppointmentType = a.AppointmentType,
                        Mode = a.Mode,
                        Status = a.Status,
                        ClientName = a.Client.User.FirstName + " " + a.Client.User.LastName,
                        TrainerName = user.FirstName + " " + user.LastName,
                        ProfessionalType = trainer.ProfessionalType
                    })
                    .ToListAsync();
            }

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

        // A client's workout and diet plans, each with the clients it could be moved to (EXT-02): the others actively
        // assigned to the plan's professional. Counts and titles only, never the plan's contents.
        private async Task<IReadOnlyList<ClientPlanViewModel>> ClientPlansAsync(int clientId)
        {
            var workout = await _db.WorkoutPlans.AsNoTracking()
                .Where(p => p.ClientId == clientId && !p.IsTemplate)
                .Select(p => new
                {
                    IsDiet = false,
                    Id = p.WorkoutPlanId,
                    p.Title,
                    p.Status,
                    p.TrainerId,
                    TrainerName = p.Trainer.User.FirstName + " " + p.Trainer.User.LastName,
                    p.StartDate,
                    HasLogs = _db.WorkoutLogs.Any(l => l.WorkoutPlanId == p.WorkoutPlanId)
                })
                .ToListAsync();
            var diet = await _db.DietPlans.AsNoTracking()
                .Where(p => p.ClientId == clientId)
                .Select(p => new
                {
                    IsDiet = true,
                    Id = p.DietPlanId,
                    p.Title,
                    p.Status,
                    p.TrainerId,
                    TrainerName = p.Trainer.User.FirstName + " " + p.Trainer.User.LastName,
                    StartDate = (DateTime?)p.StartDate,
                    HasLogs = _db.MealLogs.Any(l => l.DietPlanMeal != null && l.DietPlanMeal.DietPlanId == p.DietPlanId)
                })
                .ToListAsync();

            var trainerIds = workout.Select(p => p.TrainerId).Concat(diet.Select(p => p.TrainerId)).Distinct().ToList();
            var targets = (await _db.TrainerAssignments.AsNoTracking()
                    .Where(a => trainerIds.Contains(a.TrainerId) && a.EndDate == null && a.ClientId != clientId)
                    .Select(a => new { a.TrainerId, a.ClientId, Name = a.Client.User.FirstName + " " + a.Client.User.LastName })
                    .ToListAsync())
                .GroupBy(a => a.TrainerId)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<SelectOptionViewModel>)g
                    .OrderBy(a => a.Name)
                    .Select(a => new SelectOptionViewModel(a.ClientId, a.Name))
                    .ToList());

            return workout.Concat(diet)
                .OrderBy(p => p.Status == PlanStatus.Active ? 0 : p.Status == PlanStatus.Draft ? 1 : 2)
                .ThenByDescending(p => p.StartDate)
                .Select(p => new ClientPlanViewModel
                {
                    IsDietPlan = p.IsDiet,
                    PlanId = p.Id,
                    Title = p.Title,
                    Status = p.Status,
                    TrainerName = p.TrainerName,
                    StartDate = p.StartDate,
                    HasLogs = p.HasLogs,
                    CorrectionTargets = targets.GetValueOrDefault(p.TrainerId) ?? []
                })
                .ToList();
        }

        /// <inheritdoc />
        public async Task<bool> DeactivateTrainerAsync(string trainerUserId, string reason, string actingAdminId)
        {
            var trainer = await _db.TrainerProfiles.Include(t => t.User).FirstOrDefaultAsync(t => t.UserId == trainerUserId);
            if (trainer is null)
            {
                return false;
            }

            // BR-23: only a professional who is working (or suspended) is closed this way; a pending registration is
            // rejected instead, and a closed account stays closed.
            if (trainer.User.AccountStatus is not (AccountStatus.Active or AccountStatus.Suspended))
            {
                throw new BusinessRuleException("BR-23", "Only an active or suspended professional's account can be closed.");
            }

            var trimmedReason = reason?.Trim();
            if (string.IsNullOrEmpty(trimmedReason))
            {
                throw new BusinessRuleException("BR-23", "Give the reason for closing the account.");
            }

            // BR-23: clients are never left without a trainer; the administrator reassigns them first (UC31).
            var assigned = await _db.TrainerAssignments.CountAsync(a => a.TrainerId == trainer.TrainerId && a.EndDate == null);
            if (assigned > 0)
            {
                throw new BusinessRuleException("BR-23",
                    $"Reassign this professional's {assigned} {(assigned == 1 ? "client" : "clients")} to someone else first.");
            }

            var now = _clock.Now;
            var name = trainer.User.FullName;
            var open = await _db.Appointments.Include(a => a.Client)
                .Where(a => a.TrainerId == trainer.TrainerId && a.StartDateTime > now
                    && (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Approved || a.Status == AppointmentStatus.Rescheduled))
                .ToListAsync();

            await using var transaction = await _db.Database.BeginTransactionAsync();

            // Sessions still to come are cancelled with the reason, and each client is told so they can book someone else.
            var cancellationReason = $"{name} has left HealthTrack. {trimmedReason}".Truncate(500)!;
            foreach (var appointment in open)
            {
                if (appointment.Status == AppointmentStatus.Pending)
                {
                    appointment.Reject(cancellationReason);
                }
                else
                {
                    appointment.CancelByTrainer(cancellationReason, now);
                }

                appointment.UpdatedAt = now;
                await _notifications.NotifyAsync(appointment.Client.UserId, NotificationType.AppointmentUpdate, "Appointment cancelled",
                    $"Your {appointment.AppointmentType.GetDisplayName().ToLowerInvariant()} on {appointment.StartDateTime:dd MMM yyyy} at {appointment.StartDateTime:HH:mm} " +
                    $"with {name} was cancelled because they have left HealthTrack. You can book with another professional.",
                    $"/Client/Appointments/Details/{appointment.AppointmentId}");
            }

            // The account closes for good; a new security stamp signs them out within a minute (BR-05). Their plans,
            // feedback, past appointments and assignments stay as history, still credited to them.
            trainer.User.AccountStatus = AccountStatus.Deactivated;
            ThrowIfFailed(await _userManager.UpdateSecurityStampAsync(trainer.User));

            await _audit.LogAsync(actingAdminId, AuditAction.Update, nameof(ApplicationUser), trainer.UserId,
                $"Professional account closed: {trainer.User.Email}. {open.Count} upcoming {(open.Count == 1 ? "appointment" : "appointments")} cancelled. Reason: {trimmedReason}".Truncate(500));

            await transaction.CommitAsync();
            return true;
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

        /// <inheritdoc />
        public Task<int?> GetClientIdAsync(string userId) =>
            _db.ClientProfiles.Where(c => c.UserId == userId).Select(c => (int?)c.ClientId).FirstOrDefaultAsync();

        /// <inheritdoc />
        public Task<int?> GetTrainerIdAsync(string userId) =>
            _db.TrainerProfiles.Where(t => t.UserId == userId).Select(t => (int?)t.TrainerId).FirstOrDefaultAsync();

        /// <inheritdoc />
        public async Task<HealthProfileViewModel?> GetHealthProfileAsync(string userId)
        {
            var profile = await _db.ClientProfiles.AsNoTracking().FirstOrDefaultAsync(c => c.UserId == userId);
            return profile is null
                ? null
                : new HealthProfileViewModel
                {
                    FitnessGoal = profile.FitnessGoal,
                    ActivityLevel = profile.ActivityLevel,
                    TargetWeightKg = profile.TargetWeightKg,
                    MedicalConditions = profile.MedicalConditions,
                    Allergies = profile.Allergies,
                    CurrentMedications = profile.CurrentMedications,
                    EmergencyContactName = profile.EmergencyContactName,
                    EmergencyContactPhone = profile.EmergencyContactPhone,
                    ShareMedicalInfoWithTrainer = profile.ShareMedicalInfoWithTrainer
                };
        }

        /// <inheritdoc />
        public async Task<bool> UpdateHealthProfileAsync(string userId, HealthProfileViewModel model)
        {
            var profile = await _db.ClientProfiles.FirstOrDefaultAsync(c => c.UserId == userId);
            if (profile is null)
            {
                return false;
            }

            profile.FitnessGoal = model.FitnessGoal!.Value;
            profile.ActivityLevel = model.ActivityLevel!.Value;
            profile.TargetWeightKg = model.TargetWeightKg;
            profile.MedicalConditions = model.MedicalConditions.NullIfBlank();
            profile.Allergies = model.Allergies.NullIfBlank();
            profile.CurrentMedications = model.CurrentMedications.NullIfBlank();
            profile.EmergencyContactName = model.EmergencyContactName.NullIfBlank();
            profile.EmergencyContactPhone = model.EmergencyContactPhone.NullIfBlank();
            profile.ShareMedicalInfoWithTrainer = model.ShareMedicalInfoWithTrainer;
            profile.UpdatedAt = _clock.Now;
            await _db.SaveChangesAsync();

            // No health values in the audit row; the sharing choice is a privacy setting, so it is recorded.
            await _audit.LogAsync(userId, AuditAction.Update, nameof(ClientProfile), profile.ClientId.ToString(CultureInfo.InvariantCulture),
                $"Health profile updated; medical details {(profile.ShareMedicalInfoWithTrainer ? "shared with" : "hidden from")} the trainer.");
            return true;
        }

        /// <inheritdoc />
        public async Task<PersonalDataExport?> ExportPersonalDataAsync(string userId)
        {
            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
            var client = await _db.ClientProfiles.AsNoTracking().FirstOrDefaultAsync(c => c.UserId == userId);
            if (user is null || client is null)
            {
                return null;
            }

            var clientId = client.ClientId;
            var now = _clock.Now;

            // BR-22: everything the app holds about the client, the encrypted fields decrypted (they are the client's own).
            var data = new
            {
                ExportedAt = now,
                Account = new
                {
                    user.FirstName,
                    user.LastName,
                    user.Email,
                    user.PhoneNumber,
                    user.DateOfBirth,
                    user.Gender,
                    user.CreatedAt,
                    user.PrivacyConsentAt,
                    user.LastLoginAt
                },
                HealthProfile = new
                {
                    client.FitnessGoal,
                    client.ActivityLevel,
                    client.TargetWeightKg,
                    client.MedicalConditions,
                    client.Allergies,
                    client.CurrentMedications,
                    client.EmergencyContactName,
                    client.EmergencyContactPhone,
                    client.ShareMedicalInfoWithTrainer
                },
                NotificationSettings = await _db.NotificationPreferences.AsNoTracking()
                    .Where(p => p.UserId == userId)
                    .Select(p => new
                    {
                        p.WorkoutRemindersEnabled,
                        p.WorkoutReminderTime,
                        p.MealRemindersEnabled,
                        p.MealReminderLeadMinutes,
                        p.AppointmentRemindersEnabled,
                        p.AppointmentReminderLeadHours,
                        p.EmailEnabled
                    })
                    .FirstOrDefaultAsync(),
                TrainerAssignments = await _db.TrainerAssignments.AsNoTracking()
                    .Where(a => a.ClientId == clientId)
                    .OrderBy(a => a.StartDate)
                    .Select(a => new { Trainer = a.Trainer.User.FirstName + " " + a.Trainer.User.LastName, a.StartDate, a.EndDate, a.Notes })
                    .ToListAsync(),
                Measurements = await _db.HealthMeasurements.AsNoTracking()
                    .Where(m => m.ClientId == clientId)
                    .OrderBy(m => m.MeasuredAt)
                    .Select(m => new
                    {
                        m.MeasuredAt,
                        m.WeightKg,
                        m.HeightCm,
                        m.BodyFatPercent,
                        m.WaistCm,
                        m.RestingHeartRate,
                        m.SystolicBp,
                        m.DiastolicBp,
                        m.BloodGlucose,
                        m.Notes
                    })
                    .ToListAsync(),
                WorkoutPlans = await _db.WorkoutPlans.AsNoTracking()
                    .Where(p => p.ClientId == clientId)
                    .OrderBy(p => p.StartDate)
                    .Select(p => new
                    {
                        p.Title,
                        p.Description,
                        p.Difficulty,
                        p.Status,
                        p.StartDate,
                        p.EndDate,
                        Trainer = p.Trainer.User.FirstName + " " + p.Trainer.User.LastName,
                        Exercises = p.Exercises.OrderBy(e => e.DayOfWeek).ThenBy(e => e.SortOrder).Select(e => new
                        {
                            e.DayOfWeek,
                            Exercise = e.Exercise.Name,
                            e.Sets,
                            e.Reps,
                            e.DurationMinutes,
                            e.RestSeconds,
                            e.Notes
                        }).ToList()
                    })
                    .ToListAsync(),
                DietPlans = await _db.DietPlans.AsNoTracking()
                    .Where(p => p.ClientId == clientId)
                    .OrderBy(p => p.StartDate)
                    .Select(p => new
                    {
                        p.Title,
                        p.Description,
                        p.Status,
                        p.StartDate,
                        p.EndDate,
                        p.DailyCalorieTarget,
                        p.ProteinTargetG,
                        p.CarbsTargetG,
                        p.FatTargetG,
                        Trainer = p.Trainer.User.FirstName + " " + p.Trainer.User.LastName,
                        Meals = p.Meals.OrderBy(m => m.ScheduledTime).Select(m => new
                        {
                            m.DayOfWeek,
                            m.MealType,
                            m.ScheduledTime,
                            m.MealDescription,
                            m.TargetCalories
                        }).ToList()
                    })
                    .ToListAsync(),
                WorkoutLogs = await _db.WorkoutLogs.AsNoTracking()
                    .Where(l => l.ClientId == clientId)
                    .OrderBy(l => l.WorkoutDate)
                    .Select(l => new
                    {
                        l.WorkoutDate,
                        Plan = l.WorkoutPlan != null ? l.WorkoutPlan.Title : null,
                        l.PerceivedEffort,
                        l.Notes,
                        Entries = l.Entries.Select(e => new
                        {
                            Exercise = e.Exercise.Name,
                            e.SetsCompleted,
                            e.RepsCompleted,
                            e.WeightKg,
                            e.DurationMinutes,
                            e.DistanceKm,
                            e.CaloriesBurned
                        }).ToList()
                    })
                    .ToListAsync(),
                MealLogs = await _db.MealLogs.AsNoTracking()
                    .Where(l => l.ClientId == clientId)
                    .OrderBy(l => l.MealDate)
                    .ThenBy(l => l.MealType)
                    .Select(l => new
                    {
                        l.MealDate,
                        l.MealType,
                        l.Notes,
                        Items = l.Items.Select(i => new { Food = i.FoodItem.Name, i.Quantity, i.Calories, i.ProteinG, i.CarbsG, i.FatG }).ToList()
                    })
                    .ToListAsync(),
                Appointments = await _db.Appointments.AsNoTracking()
                    .Where(a => a.ClientId == clientId)
                    .OrderBy(a => a.StartDateTime)
                    .Select(a => new
                    {
                        Professional = a.Trainer.User.FirstName + " " + a.Trainer.User.LastName,
                        a.AppointmentType,
                        a.Mode,
                        a.StartDateTime,
                        a.DurationMinutes,
                        a.Status,
                        a.Location,
                        a.ClientReason,
                        a.ProfessionalNotes,
                        a.CancellationReason,
                        a.CreatedAt
                    })
                    .ToListAsync(),
                Feedback = await _db.Feedback.AsNoTracking()
                    .Where(f => f.ClientId == clientId)
                    .OrderBy(f => f.CreatedAt)
                    .Select(f => new { Trainer = f.Trainer.User.FirstName + " " + f.Trainer.User.LastName, f.Category, f.Rating, f.Message, f.CreatedAt })
                    .ToListAsync(),
                Notifications = await _db.Notifications.AsNoTracking()
                    .Where(n => n.UserId == userId)
                    .OrderBy(n => n.CreatedAt)
                    .Select(n => new { n.Type, n.Title, n.Message, n.IsRead, n.CreatedAt })
                    .ToListAsync()
            };

            // UC16: the export is audited, without any of its contents.
            await _audit.LogAsync(userId, AuditAction.Export, "PersonalData", userId, "Personal data exported (UC16).");
            return new PersonalDataExport($"healthtrack-my-data-{now.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}.json",
                JsonSerializer.SerializeToUtf8Bytes(data, ExportJson));
        }

        /// <inheritdoc />
        public async Task<DeleteAccountStatus> DeleteAccountAsync(string userId, string password)
        {
            var user = await _userManager.FindByIdAsync(userId);
            var client = user is null ? null : await _db.ClientProfiles.FirstOrDefaultAsync(c => c.UserId == userId);
            if (user is null || client is null)
            {
                return DeleteAccountStatus.NotFound;
            }

            // The same check as signing in, with lockout, so the form can't be used to guess the password.
            var check = await _signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
            if (check.IsLockedOut)
            {
                return DeleteAccountStatus.LockedOut;
            }

            if (!check.Succeeded)
            {
                return DeleteAccountStatus.WrongPassword;
            }

            var clientId = client.ClientId;
            await using var transaction = await _db.Database.BeginTransactionAsync();

            // BR-22: the health records go in dependency order, because most foreign keys are Restrict (spec 2.9). Feedback can
            // point at a workout log, and logs at plans and planned meals, so feedback goes first, then the logs, then the
            // plans and everything else, and the profile last. Children are loaded so EF Core deletes them on every provider.
            _db.Feedback.RemoveRange(await _db.Feedback.Where(f => f.ClientId == clientId).ToListAsync());
            await _db.SaveChangesAsync();

            _db.WorkoutLogs.RemoveRange(await _db.WorkoutLogs.Include(l => l.Entries).Where(l => l.ClientId == clientId).ToListAsync());
            _db.MealLogs.RemoveRange(await _db.MealLogs.Include(l => l.Items).Where(l => l.ClientId == clientId).ToListAsync());
            await _db.SaveChangesAsync();

            _db.WorkoutPlans.RemoveRange(await _db.WorkoutPlans.Include(p => p.Exercises).Where(p => p.ClientId == clientId).ToListAsync());
            _db.DietPlans.RemoveRange(await _db.DietPlans.Include(p => p.Meals).Where(p => p.ClientId == clientId).ToListAsync());
            _db.Appointments.RemoveRange(await _db.Appointments.Where(a => a.ClientId == clientId).ToListAsync());
            _db.HealthMeasurements.RemoveRange(await _db.HealthMeasurements.Where(m => m.ClientId == clientId).ToListAsync());
            _db.TrainerAssignments.RemoveRange(await _db.TrainerAssignments.Where(a => a.ClientId == clientId).ToListAsync());
            _db.Notifications.RemoveRange(await _db.Notifications.Where(n => n.UserId == userId).ToListAsync());
            _db.NotificationPreferences.RemoveRange(await _db.NotificationPreferences.Where(p => p.UserId == userId).ToListAsync());
            await _db.SaveChangesAsync();

            _db.ClientProfiles.Remove(client);
            await _db.SaveChangesAsync();

            // The AspNetUsers row stays, because the audit log refers to it, but it no longer identifies anyone and can never
            // sign in again.
            var placeholder = $"deleted-{user.Id}@healthtrack.invalid";
            user.FirstName = "Deleted";
            user.LastName = "user";
            user.Email = placeholder;
            user.UserName = placeholder;
            user.EmailConfirmed = false;
            user.PhoneNumber = null;
            user.PhoneNumberConfirmed = false;
            user.PasswordHash = null;
            user.DateOfBirth = DateTime.MinValue;
            user.Gender = Gender.PreferNotToSay;
            user.AccountStatus = AccountStatus.Deactivated;
            user.LockoutEnabled = true;
            user.LockoutEnd = DateTimeOffset.MaxValue;
            user.AccessFailedCount = 0;
            ThrowIfFailed(await _userManager.UpdateAsync(user));
            // BR-05: a new security stamp signs out the account's other sessions within a minute.
            ThrowIfFailed(await _userManager.UpdateSecurityStampAsync(user));

            await _audit.LogAsync(userId, AuditAction.Delete, nameof(ApplicationUser), userId,
                "Account deleted: health records removed and the user anonymised (BR-22).");
            await transaction.CommitAsync();

            await _signInManager.SignOutAsync();
            return DeleteAccountStatus.Deleted;
        }

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
