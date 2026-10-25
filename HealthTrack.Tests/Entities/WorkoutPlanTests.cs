using HealthTrack.Models.Entities;
using HealthTrack.Models.Enums;
using HealthTrack.Tests.TestSupport;

namespace HealthTrack.Tests.Entities;

/// <summary>
/// Workout plans and templates: copying (BR-12), the weekly schedule and the lifecycle (BR-09, BR-10, BR-21).
/// </summary>
[TestClass]
public sealed class WorkoutPlanTests
{
    private static WorkoutPlan NewTemplate() => new()
    {
        WorkoutPlanId = 7,
        TrainerId = 1,
        Title = "Beginner full body",
        Description = "Three easy sessions a week.",
        Difficulty = DifficultyLevel.Beginner,
        IsTemplate = true,
        Exercises =
        {
            new WorkoutPlanExercise { WorkoutPlanExerciseId = 70, ExerciseId = 12, DayOfWeek = DayOfWeek.Monday, SortOrder = 1, Sets = 3, Reps = 10, RestSeconds = 90, Notes = "Slow and controlled" },
            new WorkoutPlanExercise { WorkoutPlanExerciseId = 71, ExerciseId = 1, DayOfWeek = DayOfWeek.Wednesday, SortOrder = 1, DurationMinutes = 30 }
        }
    };

    private static WorkoutPlan NewDraftClientPlan(int exerciseCount) => new()
    {
        TrainerId = 1,
        ClientId = 3,
        StartDate = new DateTime(2026, 11, 2),
        Exercises = Enumerable.Range(1, exerciseCount)
            .Select(i => new WorkoutPlanExercise { ExerciseId = i, DayOfWeek = DayOfWeek.Monday, SortOrder = i, Sets = 3, Reps = 10 })
            .ToList()
    };

    /// <summary>The copy is a Draft client plan that records its template and starts on the given date.</summary>
    [TestMethod]
    public void CreateCopyForClient_Template_CreatesDraftClientPlan()
    {
        var copy = NewTemplate().CreateCopyForClient(clientId: 3, trainerId: 2, start: new DateTime(2026, 11, 2, 8, 30, 0));

        Assert.AreEqual(3, copy.ClientId);
        Assert.AreEqual(2, copy.TrainerId);
        Assert.AreEqual(7, copy.SourceTemplateId);
        Assert.IsFalse(copy.IsTemplate);
        Assert.AreEqual(PlanStatus.Draft, copy.Status);
        Assert.AreEqual(new DateTime(2026, 11, 2), copy.StartDate);
        Assert.AreEqual("Beginner full body", copy.Title);
        Assert.AreEqual("Three easy sessions a week.", copy.Description);
        Assert.AreEqual(DifficultyLevel.Beginner, copy.Difficulty);
        Assert.AreEqual(0, copy.WorkoutPlanId);
    }

    /// <summary>Every line is copied with the same prescription, as a new unsaved row.</summary>
    [TestMethod]
    public void CreateCopyForClient_Template_CopiesEveryLine()
    {
        var template = NewTemplate();

        var copy = template.CreateCopyForClient(3, 2, new DateTime(2026, 11, 2));

        Assert.HasCount(2, copy.Exercises);
        var original = template.Exercises.First();
        var copied = copy.Exercises.First();
        Assert.AreNotSame(original, copied);
        Assert.AreEqual(0, copied.WorkoutPlanExerciseId);
        Assert.AreEqual(original.ExerciseId, copied.ExerciseId);
        Assert.AreEqual(original.DayOfWeek, copied.DayOfWeek);
        Assert.AreEqual(original.SortOrder, copied.SortOrder);
        Assert.AreEqual(original.Sets, copied.Sets);
        Assert.AreEqual(original.Reps, copied.Reps);
        Assert.AreEqual(original.RestSeconds, copied.RestSeconds);
        Assert.AreEqual(original.Notes, copied.Notes);
        Assert.AreEqual(30, copy.Exercises.Last().DurationMinutes);
    }

    /// <summary>BR-12: copying, and later editing the copy, never changes the template.</summary>
    [TestMethod]
    public void CreateCopyForClient_CopyEdited_TemplateUnchanged()
    {
        var template = NewTemplate();

        var copy = template.CreateCopyForClient(3, 2, new DateTime(2026, 11, 2));
        copy.Exercises.First().Sets = 5;
        copy.Exercises.Add(new WorkoutPlanExercise { ExerciseId = 20, DayOfWeek = DayOfWeek.Friday, SortOrder = 1, DurationMinutes = 15 });
        copy.Title = "Renamed";

        Assert.IsTrue(template.IsTemplate);
        Assert.IsNull(template.ClientId);
        Assert.IsNull(template.StartDate);
        Assert.AreEqual(PlanStatus.Draft, template.Status);
        Assert.AreEqual("Beginner full body", template.Title);
        Assert.HasCount(2, template.Exercises);
        Assert.AreEqual(3, template.Exercises.First().Sets);
        Assert.IsEmpty(template.Copies);
    }

    /// <summary>Only templates can be copied to a client.</summary>
    [TestMethod]
    public void CreateCopyForClient_ClientPlan_ThrowsBr12()
    {
        var clientPlan = NewDraftClientPlan(1);
        clientPlan.WorkoutPlanId = 9;

        RuleAssert.Broken("BR-12", () => clientPlan.CreateCopyForClient(4, 1, new DateTime(2026, 11, 2)));
    }

    /// <summary>UC39: a client plan saved as a template keeps its lines but loses the client, dates and status.</summary>
    [TestMethod]
    public void CreateTemplateCopy_ClientPlan_CopiesLinesAsDraftTemplate()
    {
        var clientPlan = NewDraftClientPlan(3);
        clientPlan.Title = "Autumn block";
        clientPlan.Status = PlanStatus.Active;

        var template = clientPlan.CreateTemplateCopy(trainerId: 8);

        Assert.IsTrue(template.IsTemplate);
        Assert.IsNull(template.ClientId);
        Assert.IsNull(template.StartDate);
        Assert.IsNull(template.SourceTemplateId);
        Assert.AreEqual(8, template.TrainerId);
        Assert.AreEqual(PlanStatus.Draft, template.Status);
        Assert.AreEqual("Autumn block", template.Title);
        Assert.HasCount(3, template.Exercises);
        Assert.AreNotSame(clientPlan.Exercises.First(), template.Exercises.First());
        Assert.AreEqual(PlanStatus.Active, clientPlan.Status);
        Assert.HasCount(3, clientPlan.Exercises);
    }

    /// <summary>A template can't be saved as a template again.</summary>
    [TestMethod]
    public void CreateTemplateCopy_Template_ThrowsBr12()
    {
        RuleAssert.Broken("BR-12", () => NewTemplate().CreateTemplateCopy(1));
    }

    /// <summary>A day's lines come back in SortOrder; a rest day has none.</summary>
    [TestMethod]
    public void GetExercisesFor_Day_ReturnsThatDaysLinesInOrder()
    {
        var plan = new WorkoutPlan
        {
            Exercises =
            {
                new WorkoutPlanExercise { ExerciseId = 2, DayOfWeek = DayOfWeek.Monday, SortOrder = 2 },
                new WorkoutPlanExercise { ExerciseId = 3, DayOfWeek = DayOfWeek.Tuesday, SortOrder = 1 },
                new WorkoutPlanExercise { ExerciseId = 1, DayOfWeek = DayOfWeek.Monday, SortOrder = 1 }
            }
        };

        CollectionAssert.AreEqual(new[] { 1, 2 }, plan.GetExercisesFor(DayOfWeek.Monday).Select(e => e.ExerciseId).ToArray());
        Assert.IsEmpty(plan.GetExercisesFor(DayOfWeek.Sunday));
    }

    /// <summary>Each training day appears once.</summary>
    [TestMethod]
    public void GetScheduledDays_SeveralLinesPerDay_ReturnsEachDayOnce()
    {
        var plan = new WorkoutPlan
        {
            Exercises =
            {
                new WorkoutPlanExercise { DayOfWeek = DayOfWeek.Friday },
                new WorkoutPlanExercise { DayOfWeek = DayOfWeek.Monday },
                new WorkoutPlanExercise { DayOfWeek = DayOfWeek.Friday },
                new WorkoutPlanExercise { DayOfWeek = DayOfWeek.Wednesday }
            }
        };

        CollectionAssert.AreEqual(new[] { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday }, plan.GetScheduledDays());
    }

    /// <summary>A new plan starts as a draft.</summary>
    [TestMethod]
    public void Status_NewPlan_IsDraft()
    {
        Assert.AreEqual(PlanStatus.Draft, new WorkoutPlan().Status);
    }

    /// <summary>Draft → Active when the plan has an exercise.</summary>
    [TestMethod]
    public void Activate_DraftWithExercise_BecomesActive()
    {
        var plan = NewDraftClientPlan(1);

        plan.Activate();

        Assert.AreEqual(PlanStatus.Active, plan.Status);
    }

    /// <summary>BR-10: a plan needs at least one exercise before activation.</summary>
    [TestMethod]
    public void Activate_NoExercises_ThrowsBr10()
    {
        var plan = NewDraftClientPlan(0);

        RuleAssert.Broken("BR-10", plan.Activate);

        Assert.AreEqual(PlanStatus.Draft, plan.Status);
    }

    /// <summary>Only drafts can be activated.</summary>
    [TestMethod]
    [DataRow(PlanStatus.Active)]
    [DataRow(PlanStatus.Completed)]
    [DataRow(PlanStatus.Archived)]
    public void Activate_NotDraft_ThrowsBr09(PlanStatus status)
    {
        var plan = NewDraftClientPlan(1);
        plan.Status = status;

        RuleAssert.Broken("BR-09", plan.Activate);
    }

    /// <summary>Templates are assigned (copied), never activated themselves.</summary>
    [TestMethod]
    public void Activate_Template_ThrowsBr12()
    {
        RuleAssert.Broken("BR-12", NewTemplate().Activate);
    }

    /// <summary>BR-09: the previous active plan is completed.</summary>
    [TestMethod]
    public void Complete_Active_BecomesCompleted()
    {
        var plan = NewDraftClientPlan(1);
        plan.Activate();

        plan.Complete();

        Assert.AreEqual(PlanStatus.Completed, plan.Status);
    }

    /// <summary>Only an active plan can be completed.</summary>
    [TestMethod]
    public void Complete_Draft_ThrowsBr09()
    {
        RuleAssert.Broken("BR-09", NewDraftClientPlan(1).Complete);
    }

    /// <summary>BR-21: plans are archived instead of deleted, from any other state.</summary>
    [TestMethod]
    [DataRow(PlanStatus.Draft)]
    [DataRow(PlanStatus.Active)]
    [DataRow(PlanStatus.Completed)]
    public void Archive_NotArchived_BecomesArchived(PlanStatus status)
    {
        var plan = NewDraftClientPlan(1);
        plan.Status = status;

        plan.Archive();

        Assert.AreEqual(PlanStatus.Archived, plan.Status);
    }

    /// <summary>Archiving twice is refused.</summary>
    [TestMethod]
    public void Archive_AlreadyArchived_ThrowsBr21()
    {
        var plan = NewDraftClientPlan(1);
        plan.Archive();

        RuleAssert.Broken("BR-21", plan.Archive);
    }
}
