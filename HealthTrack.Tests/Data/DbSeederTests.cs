using HealthTrack.Data;
using HealthTrack.Models.Enums;
using HealthTrack.Services;
using HealthTrack.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Tests.Data;

/// <summary>
/// The demo data (Phase 8) on EF Core InMemory: eight believable weeks, the same on every run, and seeded only once.
/// The clock reads Sunday 25 October 2026, 14:00.
/// </summary>
[TestClass]
public sealed class DbSeederTests
{
    private static readonly DateTime Now = new(2026, 10, 25, 14, 0, 0);

    private static async Task<TestHost> SeededAsync()
    {
        var host = await TestHost.CreateAsync(Now);
        await DbSeeder.SeedAsync(host.Services);
        return host;
    }

    /// <summary>client@ follows "Beginner full body" from 8 weeks ago with about 80% of sessions logged, eats to a 4-meal plan.</summary>
    [TestMethod]
    public async Task SeedAsync_MainClient_HasEightWeeksOfPlansAndLogs()
    {
        await using var host = await SeededAsync();
        var client = await host.Db.ClientProfiles.SingleAsync(c => c.User.Email == "client@healthtrack.test");

        var plan = await host.Db.WorkoutPlans.Include(p => p.SourceTemplate).SingleAsync(p => p.ClientId == client.ClientId);
        Assert.AreEqual(PlanStatus.Active, plan.Status);
        Assert.AreEqual("Beginner full body", plan.SourceTemplate!.Title);
        Assert.AreEqual(Now.Date.AddDays(-56), plan.StartDate);

        var adherence = await host.Get<IProgressService>().GetAdherenceAsync(client.ClientId, plan.StartDate!.Value, Now.Date);
        Assert.IsTrue(adherence.Percent is >= 65 and <= 95, $"Adherence was {adherence.Percent}%.");

        var diet = await host.Db.DietPlans.Include(p => p.Meals).SingleAsync(p => p.ClientId == client.ClientId);
        Assert.AreEqual(PlanStatus.Active, diet.Status);
        Assert.HasCount(4, diet.Meals);
        Assert.IsGreaterThan(150, await host.Db.MealLogs.CountAsync(m => m.ClientId == client.ClientId));
        Assert.AreEqual(4, await host.Db.Feedback.CountAsync(f => f.ClientId == client.ClientId));

        // Weekly weigh-ins, falling about 0.4 kg a week, with vitals.
        var weights = await host.Db.HealthMeasurements.Where(m => m.ClientId == client.ClientId).OrderBy(m => m.MeasuredAt).ToListAsync();
        Assert.IsGreaterThanOrEqualTo(9, weights.Count);
        Assert.IsTrue(weights[^1].WeightKg < weights[0].WeightKg - 2.5m);
        Assert.IsTrue(weights.Count(m => m.RestingHeartRate is not null && m.SystolicBp is not null) >= 8);
    }

    /// <summary>client@ has appointments in every status, one of them approved and starting within 24 hours.</summary>
    [TestMethod]
    public async Task SeedAsync_MainClient_AppointmentsInEveryStatus()
    {
        await using var host = await SeededAsync();
        var appointments = await host.Db.Appointments.Where(a => a.Client.User.Email == "client@healthtrack.test").ToListAsync();

        CollectionAssert.AreEquivalent(Enum.GetValues<AppointmentStatus>(), appointments.Select(a => a.Status).Distinct().ToArray());
        Assert.IsTrue(appointments.Any(a => a.Status == AppointmentStatus.Approved && a.StartDateTime > Now && a.StartDateTime <= Now.AddHours(24)));
        Assert.IsTrue(appointments.Where(a => a.Status == AppointmentStatus.Completed).All(a => !string.IsNullOrEmpty(a.ProfessionalNotes)));
        // Nothing that has already happened is in the future, and nothing still open is in the past.
        Assert.IsTrue(appointments.Where(a => a.Status is AppointmentStatus.Completed or AppointmentStatus.NoShow).All(a => a.StartDateTime < Now));
        Assert.IsTrue(appointments.Where(a => a.Status is AppointmentStatus.Pending or AppointmentStatus.Rescheduled).All(a => a.StartDateTime > Now));
    }

    /// <summary>
    /// The other clients: client3@ has done nothing for 10+ days, client4@ has a diet plan from nutritionist@, and client5@ and
    /// client6@ are unassigned with only free logs.
    /// </summary>
    [TestMethod]
    public async Task SeedAsync_OtherClients_MatchTheDemoScript()
    {
        await using var host = await SeededAsync();
        var db = host.Db;

        var dilini = await db.ClientProfiles.SingleAsync(c => c.User.Email == "client3@healthtrack.test");
        var lastWorkout = await db.WorkoutLogs.Where(l => l.ClientId == dilini.ClientId).MaxAsync(l => l.WorkoutDate);
        var lastMeal = await db.MealLogs.Where(l => l.ClientId == dilini.ClientId).MaxAsync(l => l.MealDate);
        var lastMeasurement = await db.HealthMeasurements.Where(m => m.ClientId == dilini.ClientId).MaxAsync(m => m.MeasuredAt);
        Assert.IsTrue(new[] { lastWorkout, lastMeal, lastMeasurement.Date }.Max() <= Now.Date.AddDays(-10));

        var kasunPlan = await db.DietPlans.Include(p => p.Trainer).ThenInclude(t => t.User).SingleAsync(p => p.Client.User.Email == "client4@healthtrack.test");
        Assert.AreEqual("nutritionist@healthtrack.test", kasunPlan.Trainer.User.Email);

        foreach (var email in new[] { "client5@healthtrack.test", "client6@healthtrack.test" })
        {
            var client = await db.ClientProfiles.SingleAsync(c => c.User.Email == email);
            Assert.IsFalse(await db.TrainerAssignments.AnyAsync(a => a.ClientId == client.ClientId));
            Assert.IsFalse(await db.WorkoutPlans.AnyAsync(p => p.ClientId == client.ClientId));
            Assert.IsFalse(await db.DietPlans.AnyAsync(p => p.ClientId == client.ClientId));
            Assert.IsTrue(await db.WorkoutLogs.AnyAsync(l => l.ClientId == client.ClientId && l.WorkoutPlanId == null));
            Assert.IsTrue(await db.MealLogs.AnyAsync(l => l.ClientId == client.ClientId && l.DietPlanMealId == null));
        }
    }

    /// <summary>Logins spread over the last 14 days, some read and some unread notifications, and nothing in the future.</summary>
    [TestMethod]
    public async Task SeedAsync_LoginsAndNotifications_SpreadOverTwoWeeksAndNeverInTheFuture()
    {
        await using var host = await SeededAsync();
        var db = host.Db;

        var logins = await db.AuditLogs.Where(a => a.Action == AuditAction.Login).Select(a => a.Timestamp).ToListAsync();
        Assert.IsGreaterThan(30, logins.Count);
        Assert.IsGreaterThanOrEqualTo(10, logins.Select(t => t.Date).Distinct().Count());
        Assert.IsTrue(logins.All(t => t >= Now.Date.AddDays(-13) && t <= Now));
        Assert.IsTrue(await db.Notifications.AnyAsync(n => n.IsRead) && await db.Notifications.AnyAsync(n => !n.IsRead));

        Assert.IsFalse(await db.WorkoutLogs.AnyAsync(l => l.WorkoutDate > Now.Date || l.CreatedAt > Now));
        Assert.IsFalse(await db.MealLogs.AnyAsync(l => l.MealDate > Now.Date || l.CreatedAt > Now));
        Assert.IsFalse(await db.HealthMeasurements.AnyAsync(m => m.MeasuredAt > Now));
        Assert.IsFalse(await db.Notifications.AnyAsync(n => n.CreatedAt > Now));
        Assert.IsFalse(await db.AuditLogs.AnyAsync(a => a.Timestamp > Now));
    }

    /// <summary>Seeding again (every start of the app) adds nothing.</summary>
    [TestMethod]
    public async Task SeedAsync_RunTwice_AddsNothing()
    {
        await using var host = await SeededAsync();
        var before = (await host.Db.WorkoutLogs.CountAsync(), await host.Db.MealLogs.CountAsync(), await host.Db.Appointments.CountAsync(),
            await host.Db.AuditLogs.CountAsync(), await host.Db.Notifications.CountAsync());

        await DbSeeder.SeedAsync(host.Services);

        var after = (await host.Db.WorkoutLogs.CountAsync(), await host.Db.MealLogs.CountAsync(), await host.Db.Appointments.CountAsync(),
            await host.Db.AuditLogs.CountAsync(), await host.Db.Notifications.CountAsync());
        Assert.AreEqual(before, after);
    }
}
