using HealthTrack.Models.Enums;
using HealthTrack.Security;
using HealthTrack.Services;
using HealthTrack.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Tests.Services;

/// <summary>
/// AssignmentService: the active trainer, the BR-08 access check, unassigned clients, and assigning and ending
/// assignments (BR-06, BR-07).
/// </summary>
[TestClass]
public sealed class AssignmentServiceTests
{
    /// <summary>An active assignment gives the trainer, with their account loaded.</summary>
    [TestMethod]
    public async Task GetActiveTrainerAsync_ActiveAssignment_ReturnsTrainerWithUser()
    {
        await using var host = await TestHost.CreateAsync();
        var client = await host.CreateClientAsync("client@example.test");
        var oldTrainer = await host.CreateTrainerAsync("old@example.test");
        var trainer = await host.CreateTrainerAsync("trainer@example.test");
        await host.AssignAsync(oldTrainer, client, endDate: host.Clock.Today.AddMonths(-1));
        await host.AssignAsync(trainer, client);

        var result = await host.Get<IAssignmentService>().GetActiveTrainerAsync(client.ClientId);

        Assert.IsNotNull(result);
        Assert.AreEqual(trainer.TrainerId, result.TrainerId);
        Assert.AreEqual("trainer@example.test", result.User.Email);
    }

    /// <summary>With only ended assignments the client has no trainer.</summary>
    [TestMethod]
    public async Task GetActiveTrainerAsync_OnlyEndedAssignment_ReturnsNull()
    {
        await using var host = await TestHost.CreateAsync();
        var client = await host.CreateClientAsync("client@example.test");
        var trainer = await host.CreateTrainerAsync("trainer@example.test");
        await host.AssignAsync(trainer, client, endDate: host.Clock.Today);

        Assert.IsNull(await host.Get<IAssignmentService>().GetActiveTrainerAsync(client.ClientId));
    }

    /// <summary>BR-08: an active assignment lets the trainer reach the client.</summary>
    [TestMethod]
    public async Task IsTrainerAssignedToClientAsync_ActiveAssignment_True()
    {
        await using var host = await TestHost.CreateAsync();
        var client = await host.CreateClientAsync("client@example.test");
        var trainer = await host.CreateTrainerAsync("trainer@example.test");
        await host.AssignAsync(trainer, client);

        Assert.IsTrue(await host.Get<IAssignmentService>().IsTrainerAssignedToClientAsync(trainer.TrainerId, client.ClientId));
    }

    /// <summary>BR-08: once the assignment has ended, the trainer can no longer reach the client.</summary>
    [TestMethod]
    public async Task IsTrainerAssignedToClientAsync_EndedAssignment_False()
    {
        await using var host = await TestHost.CreateAsync();
        var client = await host.CreateClientAsync("client@example.test");
        var trainer = await host.CreateTrainerAsync("trainer@example.test");
        await host.AssignAsync(trainer, client, endDate: host.Clock.Today);

        Assert.IsFalse(await host.Get<IAssignmentService>().IsTrainerAssignedToClientAsync(trainer.TrainerId, client.ClientId));
    }

    /// <summary>BR-08: another trainer's client is out of reach.</summary>
    [TestMethod]
    public async Task IsTrainerAssignedToClientAsync_OtherTrainersClient_False()
    {
        await using var host = await TestHost.CreateAsync();
        var client = await host.CreateClientAsync("client@example.test");
        var trainer = await host.CreateTrainerAsync("trainer@example.test");
        var otherTrainer = await host.CreateTrainerAsync("other@example.test");
        await host.AssignAsync(trainer, client);

        Assert.IsFalse(await host.Get<IAssignmentService>().IsTrainerAssignedToClientAsync(otherTrainer.TrainerId, client.ClientId));
    }

    /// <summary>Unassigned means no active assignment; inactive accounts are left out; the list is ordered by name.</summary>
    [TestMethod]
    public async Task GetUnassignedClientsAsync_MixedClients_ReturnsActiveUnassignedByName()
    {
        await using var host = await TestHost.CreateAsync();
        var trainer = await host.CreateTrainerAsync("trainer@example.test");
        var assigned = await host.CreateClientAsync("assigned@example.test", "Anna");
        var previouslyAssigned = await host.CreateClientAsync("previous@example.test", "Zara");
        await host.CreateClientAsync("never@example.test", "Bella");
        await host.CreateClientAsync("suspended@example.test", "Carl", AccountStatus.Suspended);
        await host.AssignAsync(trainer, assigned);
        await host.AssignAsync(trainer, previouslyAssigned, endDate: host.Clock.Today.AddDays(-10));

        var clients = await host.Get<IAssignmentService>().GetUnassignedClientsAsync();

        CollectionAssert.AreEqual(new[] { "Bella", "Zara" }, clients.Select(c => c.User.FirstName).ToArray());
    }

    /// <summary>BR-06: assigning a new trainer ends the current assignment that day; client and both trainers are told.</summary>
    [TestMethod]
    public async Task AssignTrainerAsync_ClientWithTrainer_EndsPreviousAndStartsNew()
    {
        await using var host = await TestHost.CreateAsync();
        var client = await host.CreateClientAsync("client@example.test");
        var oldTrainer = await host.CreateTrainerAsync("old@example.test");
        var newTrainer = await host.CreateTrainerAsync("new@example.test");
        var previous = await host.AssignAsync(oldTrainer, client);
        var admin = await host.CreateUserAsync("admin@example.test", AppRoles.Admin);

        var found = await host.Get<IAssignmentService>()
            .AssignTrainerAsync(client.ClientId, newTrainer.TrainerId, host.Clock.Today, " Good match ", admin.Id);

        Assert.IsTrue(found);
        var assignments = await host.Db.TrainerAssignments.AsNoTracking().OrderBy(a => a.AssignmentId).ToListAsync();
        Assert.HasCount(2, assignments);
        Assert.AreEqual(host.Clock.Today, assignments[0].EndDate);
        Assert.AreEqual(previous.AssignmentId, assignments[0].AssignmentId);
        Assert.IsNull(assignments[1].EndDate);
        Assert.AreEqual(newTrainer.TrainerId, assignments[1].TrainerId);
        Assert.AreEqual(host.Clock.Today, assignments[1].StartDate);
        Assert.AreEqual("Good match", assignments[1].Notes);

        var notified = await host.Db.Notifications.Select(n => n.UserId).ToListAsync();
        CollectionAssert.AreEquivalent(new[] { client.UserId, newTrainer.UserId, oldTrainer.UserId }, notified);
        Assert.AreEqual("New client assigned", (await host.Db.Notifications.SingleAsync(n => n.UserId == newTrainer.UserId)).Title);

        var audit = await host.Db.AuditLogs.SingleAsync(a => a.Action == AuditAction.Assign);
        Assert.AreEqual(admin.Id, audit.UserId);
        Assert.IsTrue(await host.Get<IAssignmentService>().IsTrainerAssignedToClientAsync(newTrainer.TrainerId, client.ClientId));
        Assert.IsFalse(await host.Get<IAssignmentService>().IsTrainerAssignedToClientAsync(oldTrainer.TrainerId, client.ClientId));
    }

    /// <summary>BR-07: a trainer at capacity is refused and nothing changes.</summary>
    [TestMethod]
    public async Task AssignTrainerAsync_FullTrainer_ThrowsBr07AndChangesNothing()
    {
        await using var host = await TestHost.CreateAsync();
        var trainer = await host.CreateTrainerAsync("trainer@example.test", maxClients: 1);
        await host.AssignAsync(trainer, await host.CreateClientAsync("first@example.test"));
        var client = await host.CreateClientAsync("second@example.test");

        await RuleAssert.BrokenAsync("BR-07", () => host.Get<IAssignmentService>()
            .AssignTrainerAsync(client.ClientId, trainer.TrainerId, host.Clock.Today, null, "admin"));

        Assert.AreEqual(1, await host.Db.TrainerAssignments.CountAsync());
        Assert.IsFalse(await host.Db.Notifications.AnyAsync());
    }

    /// <summary>BR-07: only approved, active trainers can be assigned.</summary>
    [TestMethod]
    [DataRow(false, null)]
    [DataRow(true, AccountStatus.Suspended)]
    public async Task AssignTrainerAsync_UnapprovedOrInactiveTrainer_ThrowsBr07(bool approved, AccountStatus? status)
    {
        await using var host = await TestHost.CreateAsync();
        var trainer = await host.CreateTrainerAsync("trainer@example.test", approved, status);
        var client = await host.CreateClientAsync("client@example.test");

        await RuleAssert.BrokenAsync("BR-07", () => host.Get<IAssignmentService>()
            .AssignTrainerAsync(client.ClientId, trainer.TrainerId, host.Clock.Today, null, "admin"));

        Assert.IsFalse(await host.Db.TrainerAssignments.AnyAsync());
    }

    /// <summary>BR-06: assigning the trainer the client already has is refused.</summary>
    [TestMethod]
    public async Task AssignTrainerAsync_SameTrainerAgain_ThrowsBr06()
    {
        await using var host = await TestHost.CreateAsync();
        var trainer = await host.CreateTrainerAsync("trainer@example.test");
        var client = await host.CreateClientAsync("client@example.test");
        await host.AssignAsync(trainer, client);

        await RuleAssert.BrokenAsync("BR-06", () => host.Get<IAssignmentService>()
            .AssignTrainerAsync(client.ClientId, trainer.TrainerId, host.Clock.Today, null, "admin"));
    }

    /// <summary>BR-06: an assignment can't start in the future, or before the current one started.</summary>
    [TestMethod]
    [DataRow(1)]
    [DataRow(-20)]
    public async Task AssignTrainerAsync_BadStartDate_ThrowsBr06(int daysFromToday)
    {
        await using var host = await TestHost.CreateAsync();
        var oldTrainer = await host.CreateTrainerAsync("old@example.test");
        var newTrainer = await host.CreateTrainerAsync("new@example.test");
        var client = await host.CreateClientAsync("client@example.test");
        await host.AssignAsync(oldTrainer, client, startDate: host.Clock.Today.AddDays(-10));

        await RuleAssert.BrokenAsync("BR-06", () => host.Get<IAssignmentService>()
            .AssignTrainerAsync(client.ClientId, newTrainer.TrainerId, host.Clock.Today.AddDays(daysFromToday), null, "admin"));

        Assert.IsNull((await host.Db.TrainerAssignments.AsNoTracking().SingleAsync()).EndDate);
    }

    /// <summary>An unknown client or trainer answers false, so the controller can return 404.</summary>
    [TestMethod]
    public async Task AssignTrainerAsync_UnknownIds_ReturnsFalse()
    {
        await using var host = await TestHost.CreateAsync();
        var trainer = await host.CreateTrainerAsync("trainer@example.test");

        Assert.IsFalse(await host.Get<IAssignmentService>().AssignTrainerAsync(999, trainer.TrainerId, host.Clock.Today, null, "admin"));
    }

    /// <summary>Ending an assignment keeps it as history and tells both people.</summary>
    [TestMethod]
    public async Task EndAssignmentAsync_Active_EndsAndNotifiesBoth()
    {
        await using var host = await TestHost.CreateAsync();
        var trainer = await host.CreateTrainerAsync("trainer@example.test");
        var client = await host.CreateClientAsync("client@example.test");
        var assignment = await host.AssignAsync(trainer, client);

        Assert.IsTrue(await host.Get<IAssignmentService>().EndAssignmentAsync(assignment.AssignmentId, host.Clock.Today, "admin"));

        Assert.AreEqual(host.Clock.Today, (await host.Db.TrainerAssignments.AsNoTracking().SingleAsync()).EndDate);
        CollectionAssert.AreEquivalent(new[] { client.UserId, trainer.UserId }, await host.Db.Notifications.Select(n => n.UserId).ToListAsync());
        Assert.IsNull(await host.Get<IAssignmentService>().GetActiveTrainerAsync(client.ClientId));
    }

    /// <summary>BR-06: an ended assignment can't be ended again, and the end date can't be in the future.</summary>
    [TestMethod]
    public async Task EndAssignmentAsync_EndedOrFutureDate_ThrowsBr06()
    {
        await using var host = await TestHost.CreateAsync();
        var trainer = await host.CreateTrainerAsync("trainer@example.test");
        var active = await host.AssignAsync(trainer, await host.CreateClientAsync("active@example.test"));
        var ended = await host.AssignAsync(trainer, await host.CreateClientAsync("ended@example.test"), endDate: host.Clock.Today.AddDays(-1));
        var assignments = host.Get<IAssignmentService>();

        await RuleAssert.BrokenAsync("BR-06", () => assignments.EndAssignmentAsync(ended.AssignmentId, host.Clock.Today, "admin"));
        await RuleAssert.BrokenAsync("BR-06", () => assignments.EndAssignmentAsync(active.AssignmentId, host.Clock.Today.AddDays(1), "admin"));
    }

    /// <summary>Trainer load counts active clients only, for approved trainers only.</summary>
    [TestMethod]
    public async Task GetTrainerLoadAsync_MixedAssignments_CountsActiveClientsOfApprovedTrainers()
    {
        await using var host = await TestHost.CreateAsync();
        var trainer = await host.CreateTrainerAsync("trainer@example.test", maxClients: 5);
        await host.CreateTrainerAsync("pending@example.test", approved: false);
        await host.AssignAsync(trainer, await host.CreateClientAsync("a@example.test"));
        await host.AssignAsync(trainer, await host.CreateClientAsync("b@example.test"));
        await host.AssignAsync(trainer, await host.CreateClientAsync("c@example.test"), endDate: host.Clock.Today);

        var load = (await host.Get<IAssignmentService>().GetTrainerLoadAsync()).Single();

        Assert.AreEqual(trainer.TrainerId, load.TrainerId);
        Assert.AreEqual(2, load.ActiveClients);
        Assert.AreEqual(5, load.MaxClients);
        Assert.AreEqual(40, load.LoadPercent);
        Assert.IsTrue(load.HasCapacity);
    }

    /// <summary>BR-07: the form only offers approved, active trainers with room.</summary>
    [TestMethod]
    public async Task GetAssignFormAsync_Trainers_OffersOnlyApprovedActiveWithRoom()
    {
        await using var host = await TestHost.CreateAsync();
        var available = await host.CreateTrainerAsync("available@example.test", firstName: "Available");
        var full = await host.CreateTrainerAsync("full@example.test", maxClients: 1, firstName: "Full");
        await host.CreateTrainerAsync("pending@example.test", approved: false, firstName: "Pending");
        await host.CreateTrainerAsync("suspended@example.test", status: AccountStatus.Suspended, firstName: "Suspended");
        var client = await host.CreateClientAsync("client@example.test");
        await host.AssignAsync(full, await host.CreateClientAsync("other@example.test"));

        var form = await host.Get<IAssignmentService>().GetAssignFormAsync(client.ClientId);

        Assert.AreEqual(client.ClientId, form.ClientId);
        Assert.AreEqual(host.Clock.Today, form.StartDate);
        CollectionAssert.AreEqual(new[] { available.TrainerId }, form.TrainerOptions.Select(o => o.Id).ToArray());
        Assert.HasCount(2, form.ClientOptions);
    }
}
