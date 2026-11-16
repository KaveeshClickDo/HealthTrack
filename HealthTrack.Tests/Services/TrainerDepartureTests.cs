using HealthTrack.Models.Enums;
using HealthTrack.Services;
using HealthTrack.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Tests.Services;

/// <summary>
/// Closing the account of a professional who has left (EXT-01, BR-23): AccountService.DeactivateTrainerAsync and the review
/// the administrator sees first on the user's page.
/// </summary>
[TestClass]
public sealed class TrainerDepartureTests
{
    /// <summary>
    /// BR-23: after reassignment the account closes and can't sign in; upcoming sessions are cancelled (or declined, if
    /// still pending) with the reason and each client is told; the past and the plans stay credited to the professional.
    /// </summary>
    [TestMethod]
    public async Task DeactivateTrainerAsync_ClientsReassigned_ClosesAccountCancelsUpcomingAndKeepsHistory()
    {
        await using var host = await TestHost.CreateAsync();
        var trainer = await host.CreateTrainerAsync("leaving@example.test");
        var client = await host.CreateClientAsync("client@example.test");
        await host.AssignAsync(trainer, client, endDate: host.Clock.Today.AddDays(-1));
        var past = await host.AddAppointmentAsync(client, trainer, host.Clock.Now.AddDays(-5), status: AppointmentStatus.Completed);
        var confirmed = await host.AddAppointmentAsync(client, trainer, host.Clock.Now.AddDays(3));
        var pending = await host.AddAppointmentAsync(client, trainer, host.Clock.Now.AddDays(4), status: AppointmentStatus.Pending);
        var plan = await host.AddPlanAsync(trainer, client, PlanStatus.Completed);
        var accounts = host.Get<IAccountService>();

        Assert.IsTrue(await accounts.DeactivateTrainerAsync(trainer.UserId, "Moved abroad", "admin-id"));

        host.Db.ChangeTracker.Clear();
        var user = await host.Db.Users.SingleAsync(u => u.Id == trainer.UserId);
        Assert.AreEqual(AccountStatus.Deactivated, user.AccountStatus);
        Assert.AreEqual(LoginStatus.Inactive, (await accounts.LoginAsync("leaving@example.test", TestHost.Password)).Status);

        var appointments = await host.Db.Appointments.ToDictionaryAsync(a => a.AppointmentId);
        Assert.AreEqual(AppointmentStatus.Completed, appointments[past.AppointmentId].Status);
        Assert.AreEqual(AppointmentStatus.CancelledByTrainer, appointments[confirmed.AppointmentId].Status);
        Assert.AreEqual(AppointmentStatus.Rejected, appointments[pending.AppointmentId].Status);
        StringAssert.Contains(appointments[confirmed.AppointmentId].CancellationReason, "has left HealthTrack. Moved abroad");

        Assert.AreEqual(trainer.TrainerId, (await host.Db.WorkoutPlans.SingleAsync(p => p.WorkoutPlanId == plan.WorkoutPlanId)).TrainerId);
        Assert.AreEqual(2, await host.Db.Notifications.CountAsync(n => n.UserId == client.UserId && n.Type == NotificationType.AppointmentUpdate));
        var audit = await host.Db.AuditLogs.SingleAsync(a => a.EntityId == trainer.UserId);
        Assert.AreEqual(AuditAction.Update, audit.Action);
        StringAssert.Contains(audit.Details, "Reason: Moved abroad");
    }

    /// <summary>BR-23: clients are never left without a trainer, so the administrator reassigns them first.</summary>
    [TestMethod]
    public async Task DeactivateTrainerAsync_ClientStillAssigned_ThrowsBr23AndChangesNothing()
    {
        await using var host = await TestHost.CreateAsync();
        var trainer = await host.CreateTrainerAsync("leaving@example.test");
        await host.AssignAsync(trainer, await host.CreateClientAsync("client@example.test"));

        var error = await RuleAssert.BrokenAsync("BR-23",
            () => host.Get<IAccountService>().DeactivateTrainerAsync(trainer.UserId, "Moved abroad", "admin-id"));

        StringAssert.Contains(error.Message, "Reassign");
        host.Db.ChangeTracker.Clear();
        Assert.AreEqual(AccountStatus.Active, (await host.Db.Users.SingleAsync(u => u.Id == trainer.UserId)).AccountStatus);
    }

    /// <summary>BR-23: the reason is required, because the clients whose sessions are cancelled see it.</summary>
    [TestMethod]
    public async Task DeactivateTrainerAsync_NoReason_ThrowsBr23()
    {
        await using var host = await TestHost.CreateAsync();
        var trainer = await host.CreateTrainerAsync("leaving@example.test");

        await RuleAssert.BrokenAsync("BR-23", () => host.Get<IAccountService>().DeactivateTrainerAsync(trainer.UserId, "  ", "admin-id"));
    }

    /// <summary>BR-23: a pending registration is rejected through the approvals page, not closed here.</summary>
    [TestMethod]
    public async Task DeactivateTrainerAsync_PendingRegistration_ThrowsBr23()
    {
        await using var host = await TestHost.CreateAsync();
        var trainer = await host.CreateTrainerAsync("new@example.test", approved: false);

        await RuleAssert.BrokenAsync("BR-23", () => host.Get<IAccountService>().DeactivateTrainerAsync(trainer.UserId, "Not needed", "admin-id"));
    }

    /// <summary>A user who isn't a professional answers false, so the controller returns 404.</summary>
    [TestMethod]
    public async Task DeactivateTrainerAsync_NotAProfessional_ReturnsFalse()
    {
        await using var host = await TestHost.CreateAsync();
        var client = await host.CreateClientAsync("client@example.test");

        Assert.IsFalse(await host.Get<IAccountService>().DeactivateTrainerAsync(client.UserId, "Not a trainer", "admin-id"));
    }

    /// <summary>The user's page lists the clients still assigned and the open sessions to come, for the review.</summary>
    [TestMethod]
    public async Task GetUserDetailsAsync_Professional_ListsAssignedClientsAndUpcomingOpenAppointments()
    {
        await using var host = await TestHost.CreateAsync();
        var trainer = await host.CreateTrainerAsync("trainer@example.test");
        var client = await host.CreateClientAsync("client@example.test", "Amaya");
        await host.AssignAsync(trainer, client);
        await host.AddAppointmentAsync(client, trainer, host.Clock.Now.AddDays(2));
        await host.AddAppointmentAsync(client, trainer, host.Clock.Now.AddDays(6), status: AppointmentStatus.CancelledByClient);
        await host.AddAppointmentAsync(client, trainer, host.Clock.Now.AddDays(-2), status: AppointmentStatus.Completed);

        var details = await host.Get<IAccountService>().GetUserDetailsAsync(trainer.UserId, "admin-id");

        Assert.AreEqual(1, details!.Trainer!.ActiveClientNames.Count);
        StringAssert.StartsWith(details.Trainer.ActiveClientNames[0], "Amaya");
        Assert.AreEqual(1, details.Trainer.UpcomingAppointments.Count);
        Assert.AreEqual(AppointmentStatus.Approved, details.Trainer.UpcomingAppointments[0].Status);
    }
}
