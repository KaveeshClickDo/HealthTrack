using HealthTrack.Models.Enums;
using HealthTrack.Services;
using HealthTrack.Tests.TestSupport;

namespace HealthTrack.Tests.Services;

/// <summary>
/// The read side of AssignmentService: the active trainer, the BR-08 access check and unassigned clients.
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
}
