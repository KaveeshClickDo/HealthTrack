using HealthTrack.Models.Entities;
using HealthTrack.Models.Enums;
using HealthTrack.Services;
using HealthTrack.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Tests.Services;

/// <summary>
/// The administrator's controlled correction of a plan assigned to the wrong client (EXT-02, BR-24).
/// </summary>
[TestClass]
public sealed class PlanCorrectionServiceTests
{
    /// <summary>
    /// BR-24: the plan moves to the trainer's other client as a draft (the administrator never activates it), the trainer
    /// and the client who had it are told, and the move is audited with the reason.
    /// </summary>
    [TestMethod]
    public async Task MovePlanAsync_WorkoutPlanToAssignedClient_MovesAsDraftNotifiesAndAudits()
    {
        await using var host = await TestHost.CreateAsync();
        var trainer = await host.CreateTrainerAsync("trainer@example.test");
        var wrong = await host.CreateClientAsync("wrong@example.test", "Wrong");
        var right = await host.CreateClientAsync("right@example.test", "Right");
        await host.AssignAsync(trainer, wrong);
        await host.AssignAsync(trainer, right);
        var plan = await host.AddPlanAsync(trainer, wrong, PlanStatus.Active);

        var owner = await host.Get<IPlanCorrectionService>()
            .MovePlanAsync(false, plan.WorkoutPlanId, right.ClientId, "Trainer picked the wrong client", "admin-id");

        Assert.AreEqual(wrong.UserId, owner);
        host.Db.ChangeTracker.Clear();
        var saved = await host.Db.WorkoutPlans.SingleAsync();
        Assert.AreEqual(right.ClientId, saved.ClientId);
        Assert.AreEqual(PlanStatus.Draft, saved.Status);
        Assert.AreEqual(host.Clock.Now, saved.UpdatedAt);
        Assert.IsTrue(await host.Db.Notifications.AnyAsync(n => n.UserId == trainer.UserId && n.LinkUrl == $"/Trainer/WorkoutPlans/Edit/{plan.WorkoutPlanId}"));
        Assert.IsTrue(await host.Db.Notifications.AnyAsync(n => n.UserId == wrong.UserId));
        Assert.IsFalse(await host.Db.Notifications.AnyAsync(n => n.UserId == right.UserId));
        var audit = await host.Db.AuditLogs.SingleAsync();
        Assert.AreEqual(nameof(WorkoutPlan), audit.EntityName);
        StringAssert.Contains(audit.Details, "Reason: Trainer picked the wrong client");
    }

    /// <summary>BR-24 works the same for a diet plan.</summary>
    [TestMethod]
    public async Task MovePlanAsync_DietPlan_MovesToTheCorrectClient()
    {
        await using var host = await TestHost.CreateAsync();
        var trainer = await host.CreateTrainerAsync("trainer@example.test");
        var wrong = await host.CreateClientAsync("wrong@example.test");
        var right = await host.CreateClientAsync("right@example.test");
        await host.AssignAsync(trainer, wrong);
        await host.AssignAsync(trainer, right);
        var plan = await host.AddDietPlanAsync(trainer, wrong);

        await host.Get<IPlanCorrectionService>().MovePlanAsync(true, plan.DietPlanId, right.ClientId, "Wrong client chosen", "admin-id");

        host.Db.ChangeTracker.Clear();
        Assert.AreEqual(right.ClientId, (await host.Db.DietPlans.SingleAsync()).ClientId);
        Assert.AreEqual(nameof(DietPlan), (await host.Db.AuditLogs.SingleAsync()).EntityName);
    }

    /// <summary>BR-24 with BR-07: a plan can only go to a client actively assigned to its professional.</summary>
    [TestMethod]
    public async Task MovePlanAsync_ClientNotAssignedToTheTrainer_ThrowsBr24()
    {
        await using var host = await TestHost.CreateAsync();
        var trainer = await host.CreateTrainerAsync("trainer@example.test");
        var other = await host.CreateTrainerAsync("other@example.test");
        var wrong = await host.CreateClientAsync("wrong@example.test");
        var elsewhere = await host.CreateClientAsync("elsewhere@example.test");
        await host.AssignAsync(trainer, wrong);
        await host.AssignAsync(other, elsewhere);
        var plan = await host.AddPlanAsync(trainer, wrong);

        await RuleAssert.BrokenAsync("BR-24", () => host.Get<IPlanCorrectionService>()
            .MovePlanAsync(false, plan.WorkoutPlanId, elsewhere.ClientId, "Wrong client chosen", "admin-id"));
    }

    /// <summary>BR-24: a plan the client has logged against stays with them.</summary>
    [TestMethod]
    public async Task MovePlanAsync_PlanWithLogs_ThrowsBr24()
    {
        await using var host = await TestHost.CreateAsync();
        var trainer = await host.CreateTrainerAsync("trainer@example.test");
        var wrong = await host.CreateClientAsync("wrong@example.test");
        var right = await host.CreateClientAsync("right@example.test");
        await host.AssignAsync(trainer, wrong);
        await host.AssignAsync(trainer, right);
        var plan = await host.AddPlanAsync(trainer, wrong, PlanStatus.Active);
        host.Db.WorkoutLogs.Add(new WorkoutLog
        {
            ClientId = wrong.ClientId, WorkoutPlanId = plan.WorkoutPlanId, WorkoutDate = host.Clock.Today, CreatedAt = host.Clock.Now
        });
        await host.Db.SaveChangesAsync();

        await RuleAssert.BrokenAsync("BR-24", () => host.Get<IPlanCorrectionService>()
            .MovePlanAsync(false, plan.WorkoutPlanId, right.ClientId, "Wrong client chosen", "admin-id"));
    }

    /// <summary>BR-24 with BR-21: completed and archived plans are history.</summary>
    [TestMethod]
    public async Task MovePlanAsync_CompletedPlan_ThrowsBr24()
    {
        await using var host = await TestHost.CreateAsync();
        var trainer = await host.CreateTrainerAsync("trainer@example.test");
        var wrong = await host.CreateClientAsync("wrong@example.test");
        var right = await host.CreateClientAsync("right@example.test");
        await host.AssignAsync(trainer, wrong);
        await host.AssignAsync(trainer, right);
        var plan = await host.AddPlanAsync(trainer, wrong, PlanStatus.Completed);

        await RuleAssert.BrokenAsync("BR-24", () => host.Get<IPlanCorrectionService>()
            .MovePlanAsync(false, plan.WorkoutPlanId, right.ClientId, "Wrong client chosen", "admin-id"));
    }

    /// <summary>BR-24: the reason is required, and the plan must actually move somewhere else.</summary>
    [TestMethod]
    public async Task MovePlanAsync_NoReasonOrSameClient_ThrowsBr24()
    {
        await using var host = await TestHost.CreateAsync();
        var trainer = await host.CreateTrainerAsync("trainer@example.test");
        var wrong = await host.CreateClientAsync("wrong@example.test");
        var right = await host.CreateClientAsync("right@example.test");
        await host.AssignAsync(trainer, wrong);
        await host.AssignAsync(trainer, right);
        var plan = await host.AddPlanAsync(trainer, wrong);
        var corrections = host.Get<IPlanCorrectionService>();

        await RuleAssert.BrokenAsync("BR-24", () => corrections.MovePlanAsync(false, plan.WorkoutPlanId, right.ClientId, " ", "admin-id"));
        await RuleAssert.BrokenAsync("BR-24", () => corrections.MovePlanAsync(false, plan.WorkoutPlanId, wrong.ClientId, "Same client", "admin-id"));
    }

    /// <summary>A missing plan, or a template, answers null so the controller returns 404.</summary>
    [TestMethod]
    public async Task MovePlanAsync_UnknownPlanOrTemplate_ReturnsNull()
    {
        await using var host = await TestHost.CreateAsync();
        var trainer = await host.CreateTrainerAsync("trainer@example.test");
        var client = await host.CreateClientAsync("client@example.test");
        await host.AssignAsync(trainer, client);
        var template = await host.AddPlanAsync(trainer, null);
        var corrections = host.Get<IPlanCorrectionService>();

        Assert.IsNull(await corrections.MovePlanAsync(false, 999, client.ClientId, "Wrong client chosen", "admin-id"));
        Assert.IsNull(await corrections.MovePlanAsync(false, template.WorkoutPlanId, client.ClientId, "Wrong client chosen", "admin-id"));
    }

    /// <summary>The user's page shows which plans can move and where (only the professional's other clients).</summary>
    [TestMethod]
    public async Task GetUserDetailsAsync_Client_ListsPlansWithCorrectionTargets()
    {
        await using var host = await TestHost.CreateAsync();
        var trainer = await host.CreateTrainerAsync("trainer@example.test");
        var wrong = await host.CreateClientAsync("wrong@example.test");
        var right = await host.CreateClientAsync("right@example.test", "Right");
        await host.AssignAsync(trainer, wrong);
        await host.AssignAsync(trainer, right);
        await host.AddPlanAsync(trainer, wrong, PlanStatus.Active);

        var details = await host.Get<IAccountService>().GetUserDetailsAsync(wrong.UserId, "admin-id");

        var plan = details!.Client!.Plans.Single();
        Assert.IsTrue(plan.CanCorrect);
        Assert.AreEqual(right.ClientId, plan.CorrectionTargets.Single().Id);
    }
}
