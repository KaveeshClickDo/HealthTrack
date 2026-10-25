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
/// The administrator side of AccountService: creating admins, trainer approvals (UC30, BR-03) and account status
/// (UC29, BR-04, BR-05).
/// </summary>
[TestClass]
public sealed class AccountServiceAdminTests
{
    /// <summary>BR-04: an admin creates another active admin, with reminder settings, and it is audited.</summary>
    [TestMethod]
    public async Task CreateAdminAsync_ValidForm_CreatesActiveAdminAndAudits()
    {
        await using var host = await TestHost.CreateAsync();
        var actingAdmin = await host.CreateUserAsync("boss@example.test", AppRoles.Admin);

        var result = await host.Get<IAccountService>().CreateAdminAsync(new CreateAdminViewModel
        {
            FirstName = "Nimal",
            LastName = "Silva",
            Email = "nimal@example.test",
            Password = "Admin@1234",
            ConfirmPassword = "Admin@1234",
            DateOfBirth = new DateTime(1980, 5, 1),
            Gender = Gender.Male,
            AcceptPrivacy = true
        }, actingAdmin.Id);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(AccountStatus.Active, result.User.AccountStatus);
        Assert.IsTrue(await host.Get<UserManager<ApplicationUser>>().IsInRoleAsync(result.User, AppRoles.Admin));
        Assert.IsTrue(await host.Db.NotificationPreferences.AnyAsync(p => p.UserId == result.User.Id));
        var audit = await host.Db.AuditLogs.SingleAsync();
        Assert.AreEqual(AuditAction.Create, audit.Action);
        Assert.AreEqual(actingAdmin.Id, audit.UserId);
        Assert.AreEqual(result.User.Id, audit.EntityId);
    }

    /// <summary>UC30: approval sets ApprovedAt, activates the account, tells the trainer, and lets them log in.</summary>
    [TestMethod]
    public async Task ApproveTrainerAsync_Pending_ActivatesNotifiesAndAllowsLogin()
    {
        await using var host = await TestHost.CreateAsync();
        var trainer = await host.CreateTrainerAsync("physio@example.test", approved: false);
        var accounts = host.Get<IAccountService>();

        Assert.IsTrue(await accounts.ApproveTrainerAsync(trainer.TrainerId, "admin-id"));

        var saved = await host.Db.TrainerProfiles.Include(t => t.User).AsNoTracking().SingleAsync();
        Assert.AreEqual(host.Clock.Now, saved.ApprovedAt);
        Assert.AreEqual(AccountStatus.Active, saved.User.AccountStatus);
        var notification = await host.Db.Notifications.SingleAsync();
        Assert.AreEqual(trainer.UserId, notification.UserId);
        Assert.AreEqual("Your trainer account has been approved", notification.Title);
        Assert.AreEqual(AuditAction.Approve, (await host.Db.AuditLogs.SingleAsync()).Action);
        Assert.AreEqual(LoginStatus.Succeeded, (await accounts.LoginAsync("physio@example.test", TestHost.Password)).Status);
    }

    /// <summary>BR-03: a registration that isn't pending can't be approved again.</summary>
    [TestMethod]
    public async Task ApproveTrainerAsync_AlreadyApproved_ThrowsBr03()
    {
        await using var host = await TestHost.CreateAsync();
        var trainer = await host.CreateTrainerAsync("trainer@example.test");

        await RuleAssert.BrokenAsync("BR-03", () => host.Get<IAccountService>().ApproveTrainerAsync(trainer.TrainerId, "admin-id"));
    }

    /// <summary>An unknown trainer answers false, so the controller can return 404.</summary>
    [TestMethod]
    public async Task ApproveTrainerAsync_UnknownTrainer_ReturnsFalse()
    {
        await using var host = await TestHost.CreateAsync();

        Assert.IsFalse(await host.Get<IAccountService>().ApproveTrainerAsync(42, "admin-id"));
    }

    /// <summary>UC30: rejection deactivates the account, keeps the reason in the audit log and blocks login.</summary>
    [TestMethod]
    public async Task RejectTrainerAsync_Pending_DeactivatesAndAuditsReason()
    {
        await using var host = await TestHost.CreateAsync();
        var trainer = await host.CreateTrainerAsync("physio@example.test", approved: false);
        var accounts = host.Get<IAccountService>();

        Assert.IsTrue(await accounts.RejectTrainerAsync(trainer.TrainerId, " Licence number not found ", "admin-id"));

        var saved = await host.Db.TrainerProfiles.Include(t => t.User).AsNoTracking().SingleAsync();
        Assert.AreEqual(AccountStatus.Deactivated, saved.User.AccountStatus);
        Assert.IsNull(saved.ApprovedAt);
        var audit = await host.Db.AuditLogs.SingleAsync();
        Assert.AreEqual(AuditAction.Reject, audit.Action);
        StringAssert.EndsWith(audit.Details, "Licence number not found");
        Assert.AreEqual(LoginStatus.Inactive, (await accounts.LoginAsync("physio@example.test", TestHost.Password)).Status);
    }

    /// <summary>A rejection needs a reason.</summary>
    [TestMethod]
    public async Task RejectTrainerAsync_BlankReason_ThrowsBr03()
    {
        await using var host = await TestHost.CreateAsync();
        var trainer = await host.CreateTrainerAsync("physio@example.test", approved: false);

        await RuleAssert.BrokenAsync("BR-03", () => host.Get<IAccountService>().RejectTrainerAsync(trainer.TrainerId, "  ", "admin-id"));
    }

    /// <summary>BR-04: an admin can't suspend themselves.</summary>
    [TestMethod]
    public async Task SetAccountStatusAsync_SuspendSelf_ThrowsBr04()
    {
        await using var host = await TestHost.CreateAsync();
        var admin = await host.CreateUserAsync("admin@example.test", AppRoles.Admin);
        await host.CreateUserAsync("other@example.test", AppRoles.Admin);

        await RuleAssert.BrokenAsync("BR-04", () => host.Get<IAccountService>()
            .SetAccountStatusAsync(admin.Id, AccountStatus.Suspended, admin.Id));

        Assert.AreEqual(AccountStatus.Active, (await host.Db.Users.AsNoTracking().SingleAsync(u => u.Id == admin.Id)).AccountStatus);
    }

    /// <summary>
    /// BR-04: there is always an active admin. With two admins, A may suspend B; B's old session (still valid for up to a
    /// minute) can't then suspend A, the last active admin.
    /// </summary>
    [TestMethod]
    public async Task SetAccountStatusAsync_LastActiveAdmin_ThrowsBr04()
    {
        await using var host = await TestHost.CreateAsync();
        var adminA = await host.CreateUserAsync("a@example.test", AppRoles.Admin);
        var adminB = await host.CreateUserAsync("b@example.test", AppRoles.Admin);
        var accounts = host.Get<IAccountService>();

        Assert.IsTrue(await accounts.SetAccountStatusAsync(adminB.Id, AccountStatus.Suspended, adminA.Id));
        await RuleAssert.BrokenAsync("BR-04", () => accounts.SetAccountStatusAsync(adminA.Id, AccountStatus.Suspended, adminB.Id));

        var activeAdmins = await host.Db.Users.CountAsync(u => u.AccountStatus == AccountStatus.Active);
        Assert.AreEqual(1, activeAdmins);
    }

    /// <summary>BR-05: suspending changes the security stamp (signing the user out) and reactivating changes it again.</summary>
    [TestMethod]
    public async Task SetAccountStatusAsync_SuspendThenReactivate_ChangesStatusAndSecurityStamp()
    {
        await using var host = await TestHost.CreateAsync();
        var admin = await host.CreateUserAsync("admin@example.test", AppRoles.Admin);
        var client = await host.CreateUserAsync("client@example.test", AppRoles.Client);
        var accounts = host.Get<IAccountService>();
        var originalStamp = client.SecurityStamp;

        Assert.IsTrue(await accounts.SetAccountStatusAsync(client.Id, AccountStatus.Suspended, admin.Id));
        var suspended = await host.Db.Users.AsNoTracking().SingleAsync(u => u.Id == client.Id);
        Assert.AreEqual(AccountStatus.Suspended, suspended.AccountStatus);
        Assert.AreNotEqual(originalStamp, suspended.SecurityStamp);
        Assert.AreEqual(LoginStatus.Inactive, (await accounts.LoginAsync("client@example.test", TestHost.Password)).Status);

        Assert.IsTrue(await accounts.SetAccountStatusAsync(client.Id, AccountStatus.Active, admin.Id));
        var reactivated = await host.Db.Users.AsNoTracking().SingleAsync(u => u.Id == client.Id);
        Assert.AreEqual(AccountStatus.Active, reactivated.AccountStatus);
        Assert.AreNotEqual(suspended.SecurityStamp, reactivated.SecurityStamp);
        Assert.AreEqual("Your account has been reactivated", (await host.Db.Notifications.SingleAsync()).Title);
        Assert.AreEqual(2, await host.Db.AuditLogs.CountAsync(a => a.Action == AuditAction.Update && a.UserId == admin.Id));
    }

    /// <summary>BR-05: only active accounts are suspended and only suspended ones reactivated.</summary>
    [TestMethod]
    public async Task SetAccountStatusAsync_WrongStartingState_ThrowsBr05()
    {
        await using var host = await TestHost.CreateAsync();
        var admin = await host.CreateUserAsync("admin@example.test", AppRoles.Admin);
        var active = await host.CreateUserAsync("active@example.test", AppRoles.Client);
        var pending = await host.CreateTrainerAsync("pending@example.test", approved: false);
        var accounts = host.Get<IAccountService>();

        await RuleAssert.BrokenAsync("BR-05", () => accounts.SetAccountStatusAsync(active.Id, AccountStatus.Active, admin.Id));
        await RuleAssert.BrokenAsync("BR-05", () => accounts.SetAccountStatusAsync(pending.UserId, AccountStatus.Suspended, admin.Id));
        await RuleAssert.BrokenAsync("BR-05", () => accounts.SetAccountStatusAsync(pending.UserId, AccountStatus.Active, admin.Id));
    }

    /// <summary>Only Suspended and Active can be set here; deactivation belongs to rejection and account deletion.</summary>
    [TestMethod]
    public async Task SetAccountStatusAsync_Deactivated_Throws()
    {
        await using var host = await TestHost.CreateAsync();
        var client = await host.CreateUserAsync("client@example.test", AppRoles.Client);

        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() => host.Get<IAccountService>()
            .SetAccountStatusAsync(client.Id, AccountStatus.Deactivated, "admin-id"));
    }

    /// <summary>UC29: the list filters by role, status and a name or email search, ordered by name.</summary>
    [TestMethod]
    public async Task GetUsersAsync_Filters_NarrowByRoleStatusAndSearch()
    {
        await using var host = await TestHost.CreateAsync();
        await host.CreateUserAsync("zoe@example.test", AppRoles.Client, firstName: "Zoe", lastName: "Perera");
        await host.CreateUserAsync("amal@example.test", AppRoles.Client, firstName: "Amal", lastName: "Perera");
        await host.CreateUserAsync("susp@example.test", AppRoles.Client, AccountStatus.Suspended, "Sunil", "Perera");
        await host.CreateUserAsync("perera.trainer@example.test", AppRoles.Trainer, firstName: "Kamal", lastName: "Dias");
        var accounts = host.Get<IAccountService>();

        var clients = await accounts.GetUsersAsync(new UserFilterViewModel { Search = "perera", Role = AppRoles.Client, Status = AccountStatus.Active }, 1);
        var byEmail = await accounts.GetUsersAsync(new UserFilterViewModel { Search = "PERERA.TRAINER" }, 1);
        var everyone = await accounts.GetUsersAsync(new UserFilterViewModel(), 1);

        CollectionAssert.AreEqual(new[] { "Amal Perera", "Zoe Perera" }, clients.Items.Select(u => u.FullName).ToArray());
        Assert.AreEqual(AppRoles.Trainer, byEmail.Items.Single().Role);
        Assert.AreEqual(4, everyone.TotalCount);
    }

    /// <summary>The details page shows counts and the trainer's name, and flags the admin's own account.</summary>
    [TestMethod]
    public async Task GetUserDetailsAsync_Client_ReturnsTotalsAndTrainer()
    {
        await using var host = await TestHost.CreateAsync();
        var client = await host.CreateClientAsync("client@example.test");
        var trainer = await host.CreateTrainerAsync("trainer@example.test", firstName: "Chris");
        await host.AssignAsync(trainer, client);
        host.Db.HealthMeasurements.Add(new HealthMeasurement { ClientId = client.ClientId, MeasuredAt = host.Clock.Now, WeightKg = 70m, HeightCm = 170m, Notes = "Private" });
        await host.Db.SaveChangesAsync();
        var accounts = host.Get<IAccountService>();

        var details = await accounts.GetUserDetailsAsync(client.UserId, "someone-else");
        var own = await accounts.GetUserDetailsAsync(client.UserId, client.UserId);

        Assert.IsNotNull(details?.Client);
        Assert.AreEqual(AppRoles.Client, details.Role);
        Assert.AreEqual(1, details.Client.MeasurementsRecorded);
        Assert.AreEqual("Chris User", details.Client.TrainerName);
        Assert.IsNull(details.Trainer);
        Assert.IsFalse(details.IsCurrentUser);
        Assert.IsTrue(own!.IsCurrentUser);
        Assert.IsNull(await accounts.GetUserDetailsAsync("missing", "someone-else"));
    }

    /// <summary>UC30: only trainers waiting for approval are listed.</summary>
    [TestMethod]
    public async Task GetPendingTrainersAsync_MixedTrainers_ReturnsOnlyPending()
    {
        await using var host = await TestHost.CreateAsync();
        await host.CreateTrainerAsync("approved@example.test");
        var pending = await host.CreateTrainerAsync("pending@example.test", approved: false);

        var list = await host.Get<IAccountService>().GetPendingTrainersAsync();

        Assert.AreEqual(pending.TrainerId, list.Single().TrainerId);
        Assert.AreEqual("pending@example.test", list.Single().Email);
    }
}
