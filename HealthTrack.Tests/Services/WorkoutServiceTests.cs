using HealthTrack.Models.Entities;
using HealthTrack.Models.Enums;
using HealthTrack.Models.ViewModels;
using HealthTrack.Services;
using HealthTrack.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Tests.Services;

/// <summary>
/// Workout plans, templates and logging (spec 1.5, BR-08 to BR-14). The test clock reads Sunday 25 October 2026, 09:00.
/// </summary>
[TestClass]
public sealed class WorkoutServiceTests
{
    private static WorkoutPlanExercise Line(Exercise exercise, DayOfWeek day, int sortOrder = 1, int? sets = 3, int? reps = 10,
        int? minutes = null) =>
        new() { ExerciseId = exercise.ExerciseId, DayOfWeek = day, SortOrder = sortOrder, Sets = sets, Reps = reps, DurationMinutes = minutes };

    private static WorkoutPlanViewModel PlanForm(int? clientId, bool isTemplate = false) => new()
    {
        IsTemplate = isTemplate,
        ClientId = clientId,
        Title = " Spring block ",
        Difficulty = DifficultyLevel.Intermediate,
        StartDate = new DateTime(2026, 10, 26),
        EndDate = new DateTime(2026, 12, 20)
    };

    /// <summary>BR-08: a trainer can't create a plan for a client who isn't assigned to them.</summary>
    [TestMethod]
    public async Task CreatePlanAsync_UnassignedClient_ReturnsNullAndCreatesNothing()
    {
        await using var host = await TestHost.CreateAsync();
        var trainer = await host.CreateTrainerAsync("trainer@example.test");
        var otherTrainer = await host.CreateTrainerAsync("other@example.test");
        var client = await host.CreateClientAsync("client@example.test");
        await host.AssignAsync(otherTrainer, client);

        var planId = await host.Get<IWorkoutService>().CreatePlanAsync(trainer.UserId, PlanForm(client.ClientId));

        Assert.IsNull(planId);
        Assert.IsFalse(await host.Db.WorkoutPlans.AnyAsync());
    }

    /// <summary>A plan for an assigned client starts as a Draft with its dates, and is audited.</summary>
    [TestMethod]
    public async Task CreatePlanAsync_AssignedClient_CreatesDraft()
    {
        await using var host = await TestHost.CreateAsync();
        var trainer = await host.CreateTrainerAsync("trainer@example.test");
        var client = await host.CreateClientAsync("client@example.test");
        await host.AssignAsync(trainer, client);

        var planId = await host.Get<IWorkoutService>().CreatePlanAsync(trainer.UserId, PlanForm(client.ClientId));

        var plan = await host.Db.WorkoutPlans.AsNoTracking().SingleAsync();
        Assert.AreEqual(plan.WorkoutPlanId, planId);
        Assert.AreEqual("Spring block", plan.Title);
        Assert.AreEqual(PlanStatus.Draft, plan.Status);
        Assert.AreEqual(client.ClientId, plan.ClientId);
        Assert.IsFalse(plan.IsTemplate);
        Assert.AreEqual(new DateTime(2026, 10, 26), plan.StartDate);
        Assert.AreEqual(AuditAction.Create, (await host.Db.AuditLogs.SingleAsync()).Action);
    }

    /// <summary>A template has no client and no dates, whatever the form says.</summary>
    [TestMethod]
    public async Task CreatePlanAsync_Template_HasNoClientOrDates()
    {
        await using var host = await TestHost.CreateAsync();
        var trainer = await host.CreateTrainerAsync("trainer@example.test");

        await host.Get<IWorkoutService>().CreatePlanAsync(trainer.UserId, PlanForm(clientId: 99, isTemplate: true));

        var template = await host.Db.WorkoutPlans.AsNoTracking().SingleAsync();
        Assert.IsTrue(template.IsTemplate);
        Assert.IsNull(template.ClientId);
        Assert.IsNull(template.StartDate);
        Assert.IsNull(template.EndDate);
    }

    /// <summary>A new line goes after the day's last line; other days don't count.</summary>
    [TestMethod]
    public async Task AddExerciseAsync_DayWithLines_AddsAtNextSortOrder()
    {
        await using var host = await TestHost.CreateAsync();
        var trainer = await host.CreateTrainerAsync("trainer@example.test");
        var squat = await host.AddExerciseAsync("Squat");
        var plan = await host.AddPlanAsync(trainer, null, PlanStatus.Draft,
            Line(squat, DayOfWeek.Monday, 1), Line(squat, DayOfWeek.Monday, 2), Line(squat, DayOfWeek.Friday, 5));

        var added = await host.Get<IWorkoutService>().AddExerciseAsync(plan.WorkoutPlanId, trainer.UserId,
            new PlanExerciseViewModel { DayOfWeek = DayOfWeek.Monday, ExerciseId = squat.ExerciseId, DurationMinutes = 20 });

        Assert.IsTrue(added);
        var monday = await host.Db.WorkoutPlanExercises.AsNoTracking()
            .Where(l => l.WorkoutPlanId == plan.WorkoutPlanId && l.DayOfWeek == DayOfWeek.Monday)
            .OrderBy(l => l.SortOrder).Select(l => l.SortOrder).ToListAsync();
        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, monday);
    }

    /// <summary>BR-11 and BR-21: a line needs sets and reps or minutes, and an active exercise.</summary>
    [TestMethod]
    public async Task AddExerciseAsync_BadPrescriptionOrInactiveExercise_Throws()
    {
        await using var host = await TestHost.CreateAsync();
        var trainer = await host.CreateTrainerAsync("trainer@example.test");
        var squat = await host.AddExerciseAsync("Squat");
        var retired = await host.AddExerciseAsync("Retired", isActive: false);
        var plan = await host.AddPlanAsync(trainer, null);
        var workouts = host.Get<IWorkoutService>();

        await RuleAssert.BrokenAsync("BR-11", () => workouts.AddExerciseAsync(plan.WorkoutPlanId, trainer.UserId,
            new PlanExerciseViewModel { DayOfWeek = DayOfWeek.Monday, ExerciseId = squat.ExerciseId, Sets = 3 }));
        await RuleAssert.BrokenAsync("BR-21", () => workouts.AddExerciseAsync(plan.WorkoutPlanId, trainer.UserId,
            new PlanExerciseViewModel { DayOfWeek = DayOfWeek.Monday, ExerciseId = retired.ExerciseId, DurationMinutes = 10 }));

        Assert.IsFalse(await host.Db.WorkoutPlanExercises.AnyAsync());
    }

    /// <summary>Only the author edits a plan: another trainer gets "not found".</summary>
    [TestMethod]
    public async Task AddExerciseAsync_OtherTrainersPlan_ReturnsFalse()
    {
        await using var host = await TestHost.CreateAsync();
        var author = await host.CreateTrainerAsync("author@example.test");
        var other = await host.CreateTrainerAsync("other@example.test");
        var squat = await host.AddExerciseAsync("Squat");
        var plan = await host.AddPlanAsync(author, null);

        Assert.IsFalse(await host.Get<IWorkoutService>().AddExerciseAsync(plan.WorkoutPlanId, other.UserId,
            new PlanExerciseViewModel { DayOfWeek = DayOfWeek.Monday, ExerciseId = squat.ExerciseId, DurationMinutes = 10 }));
        Assert.IsNull(await host.Get<IWorkoutService>().GetPlanForEditAsync(plan.WorkoutPlanId, other.UserId));
    }

    /// <summary>
    /// BR-12: assigning a template copies its lines to a Draft client plan that records the template as its source. Any
    /// trainer may use another trainer's template for their own client, and the template is unchanged.
    /// </summary>
    [TestMethod]
    public async Task AssignTemplateAsync_AssignedClient_CopiesLinesAndRecordsSource()
    {
        await using var host = await TestHost.CreateAsync();
        var author = await host.CreateTrainerAsync("author@example.test");
        var coach = await host.CreateTrainerAsync("coach@example.test");
        var client = await host.CreateClientAsync("client@example.test");
        await host.AssignAsync(coach, client);
        var squat = await host.AddExerciseAsync("Squat");
        var run = await host.AddExerciseAsync("Run");
        var template = await host.AddPlanAsync(author, null, PlanStatus.Draft,
            Line(squat, DayOfWeek.Monday, 1, 3, 10), Line(run, DayOfWeek.Wednesday, 1, null, null, 30));

        var planId = await host.Get<IWorkoutService>().AssignTemplateAsync(template.WorkoutPlanId, client.ClientId,
            new DateTime(2026, 11, 2), activateNow: false, coach.UserId);

        var copy = await host.Db.WorkoutPlans.AsNoTracking().Include(p => p.Exercises).SingleAsync(p => p.WorkoutPlanId == planId);
        Assert.AreEqual(template.WorkoutPlanId, copy.SourceTemplateId);
        Assert.AreEqual(coach.TrainerId, copy.TrainerId);
        Assert.AreEqual(client.ClientId, copy.ClientId);
        Assert.IsFalse(copy.IsTemplate);
        Assert.AreEqual(PlanStatus.Draft, copy.Status);
        Assert.AreEqual(new DateTime(2026, 11, 2), copy.StartDate);
        CollectionAssert.AreEquivalent(
            new[] { (squat.ExerciseId, DayOfWeek.Monday, (int?)3, (int?)10, (int?)null), (run.ExerciseId, DayOfWeek.Wednesday, (int?)null, (int?)null, (int?)30) },
            copy.Exercises.Select(l => (l.ExerciseId, l.DayOfWeek, l.Sets, l.Reps, l.DurationMinutes)).ToArray());

        var original = await host.Db.WorkoutPlans.AsNoTracking().Include(p => p.Exercises).SingleAsync(p => p.WorkoutPlanId == template.WorkoutPlanId);
        Assert.IsTrue(original.IsTemplate);
        Assert.HasCount(2, original.Exercises);
    }

    /// <summary>BR-08: a template can only go to the trainer's own assigned clients.</summary>
    [TestMethod]
    public async Task AssignTemplateAsync_UnassignedClient_ReturnsNull()
    {
        await using var host = await TestHost.CreateAsync();
        var trainer = await host.CreateTrainerAsync("trainer@example.test");
        var client = await host.CreateClientAsync("client@example.test");
        var squat = await host.AddExerciseAsync("Squat");
        var template = await host.AddPlanAsync(trainer, null, PlanStatus.Draft, Line(squat, DayOfWeek.Monday));

        Assert.IsNull(await host.Get<IWorkoutService>().AssignTemplateAsync(template.WorkoutPlanId, client.ClientId,
            host.Clock.Today, activateNow: true, trainer.UserId));
        Assert.AreEqual(1, await host.Db.WorkoutPlans.CountAsync());
    }

    /// <summary>"Activate now" activates the copy and completes the client's previous plan in the same step (BR-09).</summary>
    [TestMethod]
    public async Task AssignTemplateAsync_ActivateNow_ActivatesCopyAndCompletesPrevious()
    {
        await using var host = await TestHost.CreateAsync();
        var trainer = await host.CreateTrainerAsync("trainer@example.test");
        var client = await host.CreateClientAsync("client@example.test");
        await host.AssignAsync(trainer, client);
        var squat = await host.AddExerciseAsync("Squat");
        var previous = await host.AddPlanAsync(trainer, client, PlanStatus.Active, Line(squat, DayOfWeek.Monday));
        var template = await host.AddPlanAsync(trainer, null, PlanStatus.Draft, Line(squat, DayOfWeek.Tuesday));

        var planId = await host.Get<IWorkoutService>().AssignTemplateAsync(template.WorkoutPlanId, client.ClientId,
            host.Clock.Today, activateNow: true, trainer.UserId);

        Assert.AreEqual(PlanStatus.Active, (await host.Db.WorkoutPlans.AsNoTracking().SingleAsync(p => p.WorkoutPlanId == planId)).Status);
        Assert.AreEqual(PlanStatus.Completed, (await host.Db.WorkoutPlans.AsNoTracking().SingleAsync(p => p.WorkoutPlanId == previous.WorkoutPlanId)).Status);
        Assert.AreEqual(NotificationType.PlanAssigned, (await host.Db.Notifications.SingleAsync()).Type);
    }

    /// <summary>BR-09: activating a plan completes the client's current active plan, and the client is told.</summary>
    [TestMethod]
    public async Task ActivatePlanAsync_ClientHasActivePlan_CompletesPreviousAndNotifies()
    {
        await using var host = await TestHost.CreateAsync();
        var trainer = await host.CreateTrainerAsync("trainer@example.test");
        var client = await host.CreateClientAsync("client@example.test");
        await host.AssignAsync(trainer, client);
        var squat = await host.AddExerciseAsync("Squat");
        var previous = await host.AddPlanAsync(trainer, client, PlanStatus.Active, Line(squat, DayOfWeek.Monday));
        var next = await host.AddPlanAsync(trainer, client, PlanStatus.Draft, Line(squat, DayOfWeek.Friday));

        Assert.IsTrue(await host.Get<IWorkoutService>().ActivatePlanAsync(next.WorkoutPlanId, trainer.UserId));

        var plans = await host.Db.WorkoutPlans.AsNoTracking().ToDictionaryAsync(p => p.WorkoutPlanId);
        Assert.AreEqual(PlanStatus.Completed, plans[previous.WorkoutPlanId].Status);
        Assert.AreEqual(PlanStatus.Active, plans[next.WorkoutPlanId].Status);
        Assert.AreEqual(1, plans.Values.Count(p => p.Status == PlanStatus.Active));

        var notification = await host.Db.Notifications.SingleAsync();
        Assert.AreEqual(client.UserId, notification.UserId);
        Assert.AreEqual(NotificationType.PlanAssigned, notification.Type);
        Assert.AreEqual("/Client/Workouts/MyPlan", notification.LinkUrl);
        Assert.IsTrue(await host.Db.AuditLogs.AnyAsync(a => a.Action == AuditAction.Update && a.EntityId == next.WorkoutPlanId.ToString()));
    }

    /// <summary>BR-10: a plan with no exercises can't be activated, and nothing changes.</summary>
    [TestMethod]
    public async Task ActivatePlanAsync_EmptyPlan_ThrowsBr10()
    {
        await using var host = await TestHost.CreateAsync();
        var trainer = await host.CreateTrainerAsync("trainer@example.test");
        var client = await host.CreateClientAsync("client@example.test");
        await host.AssignAsync(trainer, client);
        var squat = await host.AddExerciseAsync("Squat");
        var current = await host.AddPlanAsync(trainer, client, PlanStatus.Active, Line(squat, DayOfWeek.Monday));
        var empty = await host.AddPlanAsync(trainer, client);

        await RuleAssert.BrokenAsync("BR-10", () => host.Get<IWorkoutService>().ActivatePlanAsync(empty.WorkoutPlanId, trainer.UserId));

        Assert.AreEqual(PlanStatus.Active, (await host.Db.WorkoutPlans.AsNoTracking().SingleAsync(p => p.WorkoutPlanId == current.WorkoutPlanId)).Status);
        Assert.IsFalse(await host.Db.Notifications.AnyAsync());
    }

    /// <summary>BR-08: once the assignment ends, the trainer can no longer reach the client's plans.</summary>
    [TestMethod]
    public async Task ActivatePlanAsync_ClientNoLongerAssigned_ReturnsFalse()
    {
        await using var host = await TestHost.CreateAsync();
        var trainer = await host.CreateTrainerAsync("trainer@example.test");
        var client = await host.CreateClientAsync("client@example.test");
        await host.AssignAsync(trainer, client, endDate: host.Clock.Today);
        var squat = await host.AddExerciseAsync("Squat");
        var plan = await host.AddPlanAsync(trainer, client, PlanStatus.Draft, Line(squat, DayOfWeek.Monday));

        Assert.IsFalse(await host.Get<IWorkoutService>().ActivatePlanAsync(plan.WorkoutPlanId, trainer.UserId));
    }

    /// <summary>Editing saves details, changes kept lines and removes ticked ones.</summary>
    [TestMethod]
    public async Task UpdatePlanAsync_EditAndRemoveLines_SavesChanges()
    {
        await using var host = await TestHost.CreateAsync();
        var trainer = await host.CreateTrainerAsync("trainer@example.test");
        var squat = await host.AddExerciseAsync("Squat");
        var plan = await host.AddPlanAsync(trainer, null, PlanStatus.Draft, Line(squat, DayOfWeek.Monday, 1), Line(squat, DayOfWeek.Monday, 2));
        var lines = plan.Exercises.OrderBy(l => l.SortOrder).ToList();

        var saved = await host.Get<IWorkoutService>().UpdatePlanAsync(plan.WorkoutPlanId, trainer.UserId, new WorkoutPlanEditViewModel
        {
            IsTemplate = true,
            Title = "Renamed",
            Difficulty = DifficultyLevel.Advanced,
            Lines =
            [
                new PlanLineEditViewModel { WorkoutPlanExerciseId = lines[0].WorkoutPlanExerciseId, Sets = 5, Reps = 5, RestSeconds = 120 },
                new PlanLineEditViewModel { WorkoutPlanExerciseId = lines[1].WorkoutPlanExerciseId, Remove = true }
            ]
        });

        Assert.IsTrue(saved);
        var stored = await host.Db.WorkoutPlans.AsNoTracking().Include(p => p.Exercises).SingleAsync();
        Assert.AreEqual("Renamed", stored.Title);
        Assert.AreEqual(DifficultyLevel.Advanced, stored.Difficulty);
        var remaining = stored.Exercises.Single();
        Assert.AreEqual(5, remaining.Sets);
        Assert.AreEqual(120, remaining.RestSeconds);
    }

    /// <summary>BR-10: an active plan must keep at least one exercise; BR-21: a completed plan can't change.</summary>
    [TestMethod]
    public async Task UpdatePlanAsync_EmptyingActivePlanOrEditingCompleted_Throws()
    {
        await using var host = await TestHost.CreateAsync();
        var trainer = await host.CreateTrainerAsync("trainer@example.test");
        var client = await host.CreateClientAsync("client@example.test");
        await host.AssignAsync(trainer, client);
        var squat = await host.AddExerciseAsync("Squat");
        var active = await host.AddPlanAsync(trainer, client, PlanStatus.Active, Line(squat, DayOfWeek.Monday));
        var completed = await host.AddPlanAsync(trainer, client, PlanStatus.Completed, Line(squat, DayOfWeek.Monday));
        var workouts = host.Get<IWorkoutService>();

        WorkoutPlanEditViewModel Form(WorkoutPlan plan, bool remove) => new()
        {
            Title = "Plan",
            Difficulty = DifficultyLevel.Beginner,
            StartDate = host.Clock.Today,
            Lines = [new PlanLineEditViewModel { WorkoutPlanExerciseId = plan.Exercises.Single().WorkoutPlanExerciseId, Remove = remove, Sets = 3, Reps = 10 }]
        };

        await RuleAssert.BrokenAsync("BR-10", () => workouts.UpdatePlanAsync(active.WorkoutPlanId, trainer.UserId, Form(active, remove: true)));
        await RuleAssert.BrokenAsync("BR-21", () => workouts.UpdatePlanAsync(completed.WorkoutPlanId, trainer.UserId, Form(completed, remove: false)));
    }

    /// <summary>UC39: saving a client plan as a template copies its lines; the client plan stays as it was.</summary>
    [TestMethod]
    public async Task SaveAsTemplateAsync_ClientPlan_CreatesTemplateCopy()
    {
        await using var host = await TestHost.CreateAsync();
        var trainer = await host.CreateTrainerAsync("trainer@example.test");
        var client = await host.CreateClientAsync("client@example.test");
        await host.AssignAsync(trainer, client);
        var squat = await host.AddExerciseAsync("Squat");
        var plan = await host.AddPlanAsync(trainer, client, PlanStatus.Active, Line(squat, DayOfWeek.Monday), Line(squat, DayOfWeek.Thursday));

        var templateId = await host.Get<IWorkoutService>().SaveAsTemplateAsync(plan.WorkoutPlanId, trainer.UserId);

        var template = await host.Db.WorkoutPlans.AsNoTracking().Include(p => p.Exercises).SingleAsync(p => p.WorkoutPlanId == templateId);
        Assert.IsTrue(template.IsTemplate);
        Assert.IsNull(template.ClientId);
        Assert.IsNull(template.StartDate);
        Assert.HasCount(2, template.Exercises);
        var original = await host.Db.WorkoutPlans.AsNoTracking().SingleAsync(p => p.WorkoutPlanId == plan.WorkoutPlanId);
        Assert.AreEqual(PlanStatus.Active, original.Status);
        Assert.AreEqual(client.ClientId, original.ClientId);
    }

    /// <summary>BR-14, UC35: each entry stores MET × the latest weight × hours; the audit row has no health values.</summary>
    [TestMethod]
    public async Task LogWorkoutAsync_FreeWorkout_StoresMetTimesLatestWeightTimesHours()
    {
        await using var host = await TestHost.CreateAsync();
        var client = await host.CreateClientAsync("client@example.test");
        await host.AddMeasurementAsync(client, 80m, host.Clock.Now.AddDays(-30));
        await host.AddMeasurementAsync(client, 70m, host.Clock.Now.AddDays(-1));
        var run = await host.AddExerciseAsync("Run", metValue: 8.0m);
        var row = await host.AddExerciseAsync("Row", metValue: 7.0m);

        var logId = await host.Get<IWorkoutService>().LogWorkoutAsync(client.UserId, new LogWorkoutViewModel
        {
            WorkoutDate = host.Clock.Today,
            PerceivedEffort = 6,
            Entries =
            [
                new LogEntryViewModel { Include = true, ExerciseId = run.ExerciseId, DurationMinutes = 30, DistanceKm = 4.5m },
                new LogEntryViewModel { Include = true, ExerciseId = row.ExerciseId, DurationMinutes = 15 },
                new LogEntryViewModel { Include = false, ExerciseId = run.ExerciseId, DurationMinutes = 60 }
            ]
        });

        var log = await host.Db.WorkoutLogs.AsNoTracking().Include(l => l.Entries).SingleAsync();
        Assert.AreEqual(log.WorkoutLogId, logId);
        Assert.IsNull(log.WorkoutPlanId);
        Assert.HasCount(2, log.Entries);
        Assert.AreEqual(280.00m, log.Entries.Single(e => e.ExerciseId == run.ExerciseId).CaloriesBurned);
        Assert.AreEqual(122.50m, log.Entries.Single(e => e.ExerciseId == row.ExerciseId).CaloriesBurned);
        Assert.AreEqual(4.5m, log.Entries.Single(e => e.ExerciseId == run.ExerciseId).DistanceKm);
        var audit = await host.Db.AuditLogs.SingleAsync();
        Assert.AreEqual(AuditAction.Create, audit.Action);
        Assert.IsFalse(audit.Details!.Contains("280"));
    }

    /// <summary>BR-13: a workout can't be logged for a future date.</summary>
    [TestMethod]
    public async Task LogWorkoutAsync_FutureDate_ThrowsBr13()
    {
        await using var host = await TestHost.CreateAsync();
        var client = await host.CreateClientAsync("client@example.test");
        await host.AddMeasurementAsync(client, 70m);
        var run = await host.AddExerciseAsync("Run");

        await RuleAssert.BrokenAsync("BR-13", () => host.Get<IWorkoutService>().LogWorkoutAsync(client.UserId, new LogWorkoutViewModel
        {
            WorkoutDate = host.Clock.Today.AddDays(1),
            Entries = [new LogEntryViewModel { Include = true, ExerciseId = run.ExerciseId, DurationMinutes = 30 }]
        }));

        Assert.IsFalse(await host.Db.WorkoutLogs.AnyAsync());
    }

    /// <summary>BR-13: a workout log needs at least one ticked exercise.</summary>
    [TestMethod]
    public async Task LogWorkoutAsync_NothingTicked_ThrowsBr13()
    {
        await using var host = await TestHost.CreateAsync();
        var client = await host.CreateClientAsync("client@example.test");
        await host.AddMeasurementAsync(client, 70m);
        var run = await host.AddExerciseAsync("Run");

        await RuleAssert.BrokenAsync("BR-13", () => host.Get<IWorkoutService>().LogWorkoutAsync(client.UserId, new LogWorkoutViewModel
        {
            WorkoutDate = host.Clock.Today,
            Entries = [new LogEntryViewModel { Include = false, ExerciseId = run.ExerciseId, DurationMinutes = 30 }]
        }));

        Assert.IsFalse(await host.Db.WorkoutLogs.AnyAsync());
    }

    /// <summary>Logging against another client's plan answers "not found".</summary>
    [TestMethod]
    public async Task LogWorkoutAsync_OtherClientsPlan_ReturnsNull()
    {
        await using var host = await TestHost.CreateAsync();
        var trainer = await host.CreateTrainerAsync("trainer@example.test");
        var client = await host.CreateClientAsync("client@example.test");
        var other = await host.CreateClientAsync("other@example.test");
        await host.AddMeasurementAsync(client, 70m);
        var run = await host.AddExerciseAsync("Run");
        var othersPlan = await host.AddPlanAsync(trainer, other, PlanStatus.Active, Line(run, DayOfWeek.Sunday));

        Assert.IsNull(await host.Get<IWorkoutService>().LogWorkoutAsync(client.UserId, new LogWorkoutViewModel
        {
            WorkoutPlanId = othersPlan.WorkoutPlanId,
            WorkoutDate = host.Clock.Today,
            Entries = [new LogEntryViewModel { Include = true, ExerciseId = run.ExerciseId, DurationMinutes = 30 }]
        }));
    }

    /// <summary>BR-21: a deactivated exercise can be logged only when it is in the plan being followed.</summary>
    [TestMethod]
    public async Task LogWorkoutAsync_InactiveExercise_AllowedOnlyFromPlan()
    {
        await using var host = await TestHost.CreateAsync();
        var trainer = await host.CreateTrainerAsync("trainer@example.test");
        var client = await host.CreateClientAsync("client@example.test");
        await host.AddMeasurementAsync(client, 70m);
        var retired = await host.AddExerciseAsync("Retired", isActive: false);
        var plan = await host.AddPlanAsync(trainer, client, PlanStatus.Active, Line(retired, DayOfWeek.Sunday));
        var workouts = host.Get<IWorkoutService>();
        LogWorkoutViewModel Form(int? planId) => new()
        {
            WorkoutPlanId = planId,
            WorkoutDate = host.Clock.Today,
            Entries = [new LogEntryViewModel { Include = true, ExerciseId = retired.ExerciseId, DurationMinutes = 20 }]
        };

        await RuleAssert.BrokenAsync("BR-21", () => workouts.LogWorkoutAsync(client.UserId, Form(null)));
        Assert.IsNotNull(await workouts.LogWorkoutAsync(client.UserId, Form(plan.WorkoutPlanId)));
    }

    /// <summary>Today's rows come from today's lines: the plan's minutes, or sets × 2 when it only gives sets and reps.</summary>
    [TestMethod]
    public async Task GetTodaysWorkoutAsync_TrainingDay_PrefillsRows()
    {
        await using var host = await TestHost.CreateAsync();
        var trainer = await host.CreateTrainerAsync("trainer@example.test");
        var client = await host.CreateClientAsync("client@example.test");
        var squat = await host.AddExerciseAsync("Squat");
        var run = await host.AddExerciseAsync("Run");
        var plan = await host.AddPlanAsync(trainer, client, PlanStatus.Active,
            Line(squat, DayOfWeek.Sunday, 2, sets: 4, reps: 8),
            Line(run, DayOfWeek.Sunday, 1, sets: null, reps: null, minutes: 25),
            Line(run, DayOfWeek.Monday, 1, sets: null, reps: null, minutes: 40));

        var today = await host.Get<IWorkoutService>().GetTodaysWorkoutAsync(client.ClientId, host.Clock.Today);

        Assert.IsTrue(today.HasActivePlan);
        Assert.IsFalse(today.IsRestDay);
        Assert.AreEqual(plan.WorkoutPlanId, today.WorkoutPlanId);
        Assert.AreEqual(host.Clock.Today, today.WorkoutDate);
        CollectionAssert.AreEqual(new[] { run.ExerciseId, squat.ExerciseId }, today.Entries.Select(e => e.ExerciseId!.Value).ToArray());
        Assert.AreEqual(25, today.Entries[0].DurationMinutes);
        Assert.AreEqual(8, today.Entries[1].DurationMinutes);
        Assert.AreEqual("4 × 8", today.Entries[1].Target);
        Assert.IsTrue(today.Entries.All(e => e.Include));
    }

    /// <summary>No rows on a rest day, and none before the plan's start date.</summary>
    [TestMethod]
    public async Task GetTodaysWorkoutAsync_RestDayOrNotStarted_NoRows()
    {
        await using var host = await TestHost.CreateAsync();
        var trainer = await host.CreateTrainerAsync("trainer@example.test");
        var client = await host.CreateClientAsync("client@example.test");
        var run = await host.AddExerciseAsync("Run");
        var plan = await host.AddPlanAsync(trainer, client, PlanStatus.Active, Line(run, DayOfWeek.Monday, minutes: 20));
        var workouts = host.Get<IWorkoutService>();

        var restDay = await workouts.GetTodaysWorkoutAsync(client.ClientId, host.Clock.Today);
        Assert.IsTrue(restDay.HasActivePlan);
        Assert.IsTrue(restDay.IsRestDay);
        Assert.IsEmpty(restDay.Entries);

        plan.StartDate = host.Clock.Today.AddDays(7);
        await host.Db.SaveChangesAsync();
        var beforeStart = await workouts.GetTodaysWorkoutAsync(client.ClientId, host.Clock.Today.AddDays(1));
        Assert.IsFalse(beforeStart.HasActivePlan);
        Assert.IsEmpty(beforeStart.Entries);
    }

    /// <summary>UC08: the week runs Monday to Sunday with each day's lines in order; today is flagged.</summary>
    [TestMethod]
    public async Task GetWeekAsync_ActivePlan_ReturnsMondayToSunday()
    {
        await using var host = await TestHost.CreateAsync();
        var trainer = await host.CreateTrainerAsync("trainer@example.test");
        var client = await host.CreateClientAsync("client@example.test");
        var squat = await host.AddExerciseAsync("Squat");
        var run = await host.AddExerciseAsync("Run");
        await host.AddPlanAsync(trainer, client, PlanStatus.Active,
            Line(squat, DayOfWeek.Monday, 2), Line(run, DayOfWeek.Monday, 1, minutes: 10), Line(run, DayOfWeek.Sunday, 1, minutes: 30));

        var week = await host.Get<IWorkoutService>().GetWeekAsync(client.ClientId);

        Assert.IsTrue(week.HasPlan);
        CollectionAssert.AreEqual(
            new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday },
            week.Days.Select(d => d.DayOfWeek).ToArray());
        CollectionAssert.AreEqual(new[] { "Run", "Squat" }, week.Days[0].Lines.Select(l => l.ExerciseName).ToArray());
        Assert.IsTrue(week.Days[1].IsRestDay);
        Assert.IsTrue(week.Days[6].IsToday);
        Assert.IsFalse((await host.Get<IWorkoutService>().GetWeekAsync(9999)).HasPlan);
    }

    /// <summary>History shows each session's entries, totals and feedback, newest first.</summary>
    [TestMethod]
    public async Task GetHistoryAsync_Sessions_IncludeEntriesTotalsAndFeedback()
    {
        await using var host = await TestHost.CreateAsync();
        var trainer = await host.CreateTrainerAsync("trainer@example.test");
        var client = await host.CreateClientAsync("client@example.test");
        await host.AddMeasurementAsync(client, 60m);
        var run = await host.AddExerciseAsync("Run", metValue: 10m);
        var workouts = host.Get<IWorkoutService>();
        var older = await workouts.LogWorkoutAsync(client.UserId, new LogWorkoutViewModel
        {
            WorkoutDate = host.Clock.Today.AddDays(-3),
            Entries = [new LogEntryViewModel { Include = true, ExerciseId = run.ExerciseId, DurationMinutes = 30 }]
        });
        await workouts.LogWorkoutAsync(client.UserId, new LogWorkoutViewModel
        {
            WorkoutDate = host.Clock.Today,
            Entries = [new LogEntryViewModel { Include = true, ExerciseId = run.ExerciseId, DurationMinutes = 20 }, new LogEntryViewModel { Include = true, ExerciseId = run.ExerciseId, DurationMinutes = 10 }]
        });
        host.Db.Feedback.Add(new Feedback { TrainerId = trainer.TrainerId, ClientId = client.ClientId, WorkoutLogId = older, Category = FeedbackCategory.Workout, Message = "Nice pace", CreatedAt = host.Clock.Now });
        await host.Db.SaveChangesAsync();

        var history = await workouts.GetHistoryAsync(client.ClientId, 1);

        Assert.HasCount(2, history.Items);
        Assert.AreEqual(host.Clock.Today, history.Items[0].WorkoutDate);
        Assert.AreEqual(30, history.Items[0].TotalMinutes);
        Assert.AreEqual(300.00m, history.Items[0].TotalCalories);
        Assert.AreEqual("Nice pace", history.Items[1].Feedback.Single().Message);
        Assert.AreEqual("Run", history.Items[1].Entries.Single().ExerciseName);
    }
}
