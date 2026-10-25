using HealthTrack.Models.Entities;
using HealthTrack.Models.Enums;
using HealthTrack.Models.ViewModels;
using HealthTrack.Security;
using HealthTrack.Services;
using HealthTrack.Tests.TestSupport;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Tests.Services;

/// <summary>
/// Registration (UC01, UC02), login with lockout and status checks (UC03, BR-03, BR-05) and the user's own account (UC05).
/// </summary>
[TestClass]
public sealed class AccountServiceTests
{
    private static RegisterClientViewModel ClientForm(string email = "amaya@example.test") => new()
    {
        FirstName = "Amaya",
        LastName = "Perera",
        Email = email,
        Password = "Client@123",
        ConfirmPassword = "Client@123",
        DateOfBirth = new DateTime(1994, 3, 12),
        Gender = Gender.Female,
        PhoneNumber = " +94 77 234 5678 ",
        AcceptPrivacy = true,
        FitnessGoal = FitnessGoal.LoseWeight,
        ActivityLevel = ActivityLevel.ModeratelyActive,
        HeightCm = 165.0m,
        WeightKg = 72.5m,
        TargetWeightKg = 64m,
        Allergies = "Peanuts",
        MedicalConditions = "   "
    };

    private static RegisterTrainerViewModel TrainerForm() => new()
    {
        FirstName = "Daniel",
        LastName = "Perera",
        Email = "daniel@example.test",
        Password = "Trainer@123",
        ConfirmPassword = "Trainer@123",
        DateOfBirth = new DateTime(1993, 2, 21),
        Gender = Gender.Male,
        AcceptPrivacy = true,
        ProfessionalType = ProfessionalType.Physiotherapist,
        Specialization = "Sports injury rehabilitation",
        Qualifications = "BSc (Hons) Physiotherapy",
        LicenseNumber = "PT-33018",
        YearsOfExperience = 4,
        Bio = "Rehabilitation after sports injuries."
    };

    /// <summary>UC01: an active Client account with its profile, first measurement, reminder settings and audit row.</summary>
    [TestMethod]
    public async Task RegisterClientAsync_ValidForm_CreatesActiveClientWithProfileMeasurementAndPreferences()
    {
        await using var host = await TestHost.CreateAsync();

        var result = await host.Get<IAccountService>().RegisterClientAsync(ClientForm());

        Assert.IsTrue(result.Succeeded);
        var user = result.User;
        Assert.AreEqual("amaya@example.test", user.UserName);
        Assert.AreEqual(AccountStatus.Active, user.AccountStatus);
        Assert.AreEqual("+94 77 234 5678", user.PhoneNumber);
        Assert.AreEqual(host.Clock.Now, user.PrivacyConsentAt);
        Assert.IsTrue(await host.Get<UserManager<ApplicationUser>>().IsInRoleAsync(user, AppRoles.Client));

        var profile = await host.Db.ClientProfiles.Include(c => c.Measurements).SingleAsync();
        Assert.AreEqual(user.Id, profile.UserId);
        Assert.AreEqual(FitnessGoal.LoseWeight, profile.FitnessGoal);
        Assert.AreEqual(64m, profile.TargetWeightKg);
        Assert.AreEqual("Peanuts", profile.Allergies);
        Assert.IsNull(profile.MedicalConditions);
        Assert.IsTrue(profile.ShareMedicalInfoWithTrainer);

        var measurement = profile.Measurements.Single();
        Assert.AreEqual(72.5m, measurement.WeightKg);
        Assert.AreEqual(165.0m, measurement.HeightCm);
        Assert.AreEqual(host.Clock.Now, measurement.MeasuredAt);

        Assert.IsTrue(await host.Db.NotificationPreferences.AnyAsync(p => p.UserId == user.Id));
        var audit = await host.Db.AuditLogs.SingleAsync();
        Assert.AreEqual(AuditAction.Register, audit.Action);
        Assert.AreEqual(user.Id, audit.UserId);
        Assert.AreEqual(nameof(ClientProfile), audit.EntityName);
    }

    /// <summary>A taken email fails with Identity's error and creates nothing more.</summary>
    [TestMethod]
    public async Task RegisterClientAsync_DuplicateEmail_FailsAndCreatesNoSecondProfile()
    {
        await using var host = await TestHost.CreateAsync();
        var accounts = host.Get<IAccountService>();
        await accounts.RegisterClientAsync(ClientForm());

        var result = await accounts.RegisterClientAsync(ClientForm("AMAYA@example.test"));

        Assert.IsFalse(result.Succeeded);
        Assert.IsTrue(result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.DuplicateEmail)));
        Assert.AreEqual(1, await host.Db.ClientProfiles.CountAsync());
        Assert.AreEqual(1, await host.Db.Users.CountAsync());
    }

    /// <summary>UC02, BR-03: a pending trainer with a profile and capacity 20; every active admin is told, a suspended one isn't.</summary>
    [TestMethod]
    public async Task RegisterTrainerAsync_ValidForm_CreatesPendingTrainerAndNotifiesActiveAdmins()
    {
        await using var host = await TestHost.CreateAsync();
        var admin1 = await host.CreateUserAsync("admin1@example.test", AppRoles.Admin);
        var admin2 = await host.CreateUserAsync("admin2@example.test", AppRoles.Admin);
        await host.CreateUserAsync("admin3@example.test", AppRoles.Admin, AccountStatus.Suspended);

        var result = await host.Get<IAccountService>().RegisterTrainerAsync(TrainerForm());

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(AccountStatus.PendingApproval, result.User.AccountStatus);
        Assert.IsTrue(await host.Get<UserManager<ApplicationUser>>().IsInRoleAsync(result.User, AppRoles.Trainer));

        var profile = await host.Db.TrainerProfiles.SingleAsync();
        Assert.AreEqual(ProfessionalType.Physiotherapist, profile.ProfessionalType);
        Assert.AreEqual("PT-33018", profile.LicenseNumber);
        Assert.AreEqual(20, profile.MaxClients);
        Assert.IsNull(profile.ApprovedAt);
        Assert.IsTrue(await host.Db.NotificationPreferences.AnyAsync(p => p.UserId == result.User.Id));

        var notified = await host.Db.Notifications.Select(n => n.UserId).OrderBy(id => id).ToListAsync();
        CollectionAssert.AreEquivalent(new[] { admin1.Id, admin2.Id }, notified);
        var notification = await host.Db.Notifications.FirstAsync();
        Assert.AreEqual(NotificationType.AccountUpdate, notification.Type);
        Assert.AreEqual("New trainer registration", notification.Title);
        Assert.AreEqual("/Admin/TrainerApprovals", notification.LinkUrl);
        Assert.AreEqual("Daniel Perera registered as a physiotherapist and is waiting for approval.", notification.Message);
    }

    /// <summary>A correct password signs in: the role for the redirect, LastLoginAt from the clock and a Login audit row.</summary>
    [TestMethod]
    public async Task LoginAsync_CorrectPassword_SignsInAndAudits()
    {
        await using var host = await TestHost.CreateAsync();
        var user = await host.CreateUserAsync("client@example.test", AppRoles.Client);

        var result = await host.Get<IAccountService>().LoginAsync(" client@example.test ", TestHost.Password);

        Assert.AreEqual(LoginStatus.Succeeded, result.Status);
        Assert.AreEqual(AppRoles.Client, result.Role);
        Assert.AreEqual(host.Clock.Now, (await host.Db.Users.SingleAsync(u => u.Id == user.Id)).LastLoginAt);
        Assert.AreEqual(AuditAction.Login, (await host.Db.AuditLogs.SingleAsync()).Action);
    }

    /// <summary>An unknown email gets the same answer as a wrong password and is audited without a user.</summary>
    [TestMethod]
    public async Task LoginAsync_UnknownEmail_InvalidCredentialsAuditedWithoutUser()
    {
        await using var host = await TestHost.CreateAsync();

        var result = await host.Get<IAccountService>().LoginAsync("nobody@example.test", "Whatever1!");

        Assert.AreEqual(LoginStatus.InvalidCredentials, result.Status);
        var audit = await host.Db.AuditLogs.SingleAsync();
        Assert.AreEqual(AuditAction.LoginFailed, audit.Action);
        Assert.IsNull(audit.UserId);
    }

    /// <summary>A wrong password is refused and audited against the user.</summary>
    [TestMethod]
    public async Task LoginAsync_WrongPassword_InvalidCredentialsAudited()
    {
        await using var host = await TestHost.CreateAsync();
        var user = await host.CreateUserAsync("client@example.test", AppRoles.Client);

        var result = await host.Get<IAccountService>().LoginAsync("client@example.test", "Wrong-Pass1");

        Assert.AreEqual(LoginStatus.InvalidCredentials, result.Status);
        var audit = await host.Db.AuditLogs.SingleAsync();
        Assert.AreEqual(AuditAction.LoginFailed, audit.Action);
        Assert.AreEqual(user.Id, audit.UserId);
    }

    /// <summary>
    /// Spec 1.9: the fifth wrong password locks the account (audited once as AccountLocked), after which even the right
    /// password is refused. No audit row ever contains a password.
    /// </summary>
    [TestMethod]
    public async Task LoginAsync_FifthWrongPassword_LocksAccount()
    {
        await using var host = await TestHost.CreateAsync();
        await host.CreateUserAsync("client@example.test", AppRoles.Client);
        var accounts = host.Get<IAccountService>();
        const string wrongPassword = "Wrong-Pass1";

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            Assert.AreEqual(LoginStatus.InvalidCredentials, (await accounts.LoginAsync("client@example.test", wrongPassword)).Status);
        }

        Assert.AreEqual(LoginStatus.LockedOut, (await accounts.LoginAsync("client@example.test", wrongPassword)).Status);
        Assert.AreEqual(LoginStatus.LockedOut, (await accounts.LoginAsync("client@example.test", TestHost.Password)).Status);

        var audits = await host.Db.AuditLogs.ToListAsync();
        Assert.AreEqual(1, audits.Count(a => a.Action == AuditAction.AccountLocked));
        Assert.AreEqual(6, audits.Count(a => a.Action == AuditAction.LoginFailed));
        Assert.IsFalse(audits.Any(a => a.Action == AuditAction.Login));
        Assert.IsFalse(audits.Any(a => a.Details != null
            && (a.Details.Contains(wrongPassword) || a.Details.Contains(TestHost.Password))));
    }

    /// <summary>BR-03: a pending trainer with the right password gets the pending message and isn't signed in.</summary>
    [TestMethod]
    public async Task LoginAsync_PendingTrainerCorrectPassword_PendingApproval()
    {
        await using var host = await TestHost.CreateAsync();
        await host.CreateUserAsync("physio@example.test", AppRoles.Trainer, AccountStatus.PendingApproval);

        var result = await host.Get<IAccountService>().LoginAsync("physio@example.test", TestHost.Password);

        Assert.AreEqual(LoginStatus.PendingApproval, result.Status);
        Assert.IsNull(result.Role);
        Assert.IsFalse(await host.Db.AuditLogs.AnyAsync(a => a.Action == AuditAction.Login));
    }

    /// <summary>The status is only revealed with the right password: a wrong one gets the usual answer.</summary>
    [TestMethod]
    public async Task LoginAsync_PendingTrainerWrongPassword_InvalidCredentials()
    {
        await using var host = await TestHost.CreateAsync();
        await host.CreateUserAsync("physio@example.test", AppRoles.Trainer, AccountStatus.PendingApproval);

        var result = await host.Get<IAccountService>().LoginAsync("physio@example.test", "Wrong-Pass1");

        Assert.AreEqual(LoginStatus.InvalidCredentials, result.Status);
    }

    /// <summary>BR-05: suspended and deactivated accounts can't sign in.</summary>
    [TestMethod]
    [DataRow(AccountStatus.Suspended)]
    [DataRow(AccountStatus.Deactivated)]
    public async Task LoginAsync_InactiveAccount_Inactive(AccountStatus status)
    {
        await using var host = await TestHost.CreateAsync();
        await host.CreateUserAsync("client@example.test", AppRoles.Client, status);

        var result = await host.Get<IAccountService>().LoginAsync("client@example.test", TestHost.Password);

        Assert.AreEqual(LoginStatus.Inactive, result.Status);
        Assert.IsFalse(await host.Db.AuditLogs.AnyAsync(a => a.Action == AuditAction.Login));
    }

    /// <summary>Logging out is audited.</summary>
    [TestMethod]
    public async Task LogoutAsync_SignedInUser_AuditsLogout()
    {
        await using var host = await TestHost.CreateAsync();
        var user = await host.CreateUserAsync("client@example.test", AppRoles.Client);

        await host.Get<IAccountService>().LogoutAsync(user.Id);

        var audit = await host.Db.AuditLogs.SingleAsync();
        Assert.AreEqual(AuditAction.Logout, audit.Action);
        Assert.AreEqual(user.Id, audit.UserId);
    }

    /// <summary>UC05: personal details are saved, and the audit row says what changed without the values.</summary>
    [TestMethod]
    public async Task UpdateProfileAsync_ValidForm_SavesAndAuditsWithoutValues()
    {
        await using var host = await TestHost.CreateAsync();
        var user = await host.CreateUserAsync("client@example.test", AppRoles.Client);

        var result = await host.Get<IAccountService>().UpdateProfileAsync(user.Id, new ProfileViewModel
        {
            FirstName = " Nimali ",
            LastName = "Silva",
            PhoneNumber = "",
            DateOfBirth = new DateTime(1992, 5, 6),
            Gender = Gender.Female
        });

        Assert.IsTrue(result.Succeeded);
        var saved = await host.Db.Users.SingleAsync(u => u.Id == user.Id);
        Assert.AreEqual("Nimali", saved.FirstName);
        Assert.IsNull(saved.PhoneNumber);
        Assert.AreEqual(Gender.Female, saved.Gender);
        var audit = await host.Db.AuditLogs.SingleAsync();
        Assert.AreEqual(AuditAction.Update, audit.Action);
        Assert.IsFalse(audit.Details!.Contains("Nimali"));
    }

    /// <summary>UC05: the current password must be right.</summary>
    [TestMethod]
    public async Task ChangePasswordAsync_WrongCurrentPassword_PasswordMismatch()
    {
        await using var host = await TestHost.CreateAsync();
        var user = await host.CreateUserAsync("client@example.test", AppRoles.Client);

        var result = await host.Get<IAccountService>().ChangePasswordAsync(user.Id, new ChangePasswordViewModel
        {
            CurrentPassword = "Not-The-One1",
            NewPassword = "Brand-New1",
            ConfirmNewPassword = "Brand-New1"
        });

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(nameof(IdentityErrorDescriber.PasswordMismatch), result.Errors.Single().Code);
        Assert.IsFalse(await host.Db.AuditLogs.AnyAsync());
    }

    /// <summary>UC05: with the right current password the new one works for the next login, and the change is audited.</summary>
    [TestMethod]
    public async Task ChangePasswordAsync_CorrectCurrentPassword_ChangesAndAudits()
    {
        await using var host = await TestHost.CreateAsync();
        var user = await host.CreateUserAsync("client@example.test", AppRoles.Client);
        var accounts = host.Get<IAccountService>();

        var result = await accounts.ChangePasswordAsync(user.Id, new ChangePasswordViewModel
        {
            CurrentPassword = TestHost.Password,
            NewPassword = "Brand-New1",
            ConfirmNewPassword = "Brand-New1"
        });

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(AuditAction.Update, (await host.Db.AuditLogs.SingleAsync()).Action);
        Assert.AreEqual(LoginStatus.Succeeded, (await accounts.LoginAsync("client@example.test", "Brand-New1")).Status);
    }
}
