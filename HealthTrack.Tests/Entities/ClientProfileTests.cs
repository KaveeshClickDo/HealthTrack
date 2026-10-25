using HealthTrack.Models.Entities;
using HealthTrack.Models.Enums;

namespace HealthTrack.Tests.Entities;

/// <summary>
/// The "current" lookups over a client's loaded collections (BR-06, BR-09).
/// </summary>
[TestClass]
public sealed class ClientProfileTests
{
    /// <summary>Medical details are shared with the trainer unless the client turns it off.</summary>
    [TestMethod]
    public void ShareMedicalInfoWithTrainer_NewProfile_IsTrue()
    {
        Assert.IsTrue(new ClientProfile().ShareMedicalInfoWithTrainer);
    }

    /// <summary>The active assignment is the one without an end date.</summary>
    [TestMethod]
    public void GetActiveAssignment_HistoryAndCurrent_ReturnsCurrent()
    {
        var current = new TrainerAssignment { AssignmentId = 2, StartDate = new DateTime(2026, 7, 1) };
        var client = new ClientProfile
        {
            Assignments = { new TrainerAssignment { AssignmentId = 1, StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 6, 30) }, current }
        };

        Assert.AreSame(current, client.GetActiveAssignment());
    }

    /// <summary>An unassigned client has no active assignment.</summary>
    [TestMethod]
    public void GetActiveAssignment_OnlyEnded_ReturnsNull()
    {
        var client = new ClientProfile
        {
            Assignments = { new TrainerAssignment { StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 6, 30) } }
        };

        Assert.IsNull(client.GetActiveAssignment());
    }

    /// <summary>The active workout plan is found among drafts and completed plans.</summary>
    [TestMethod]
    public void GetActiveWorkoutPlan_MixedStatuses_ReturnsActive()
    {
        var active = new WorkoutPlan { Status = PlanStatus.Active };
        var client = new ClientProfile
        {
            WorkoutPlans = { new WorkoutPlan { Status = PlanStatus.Completed }, active, new WorkoutPlan { Status = PlanStatus.Draft } }
        };

        Assert.AreSame(active, client.GetActiveWorkoutPlan());
        Assert.IsNull(new ClientProfile().GetActiveWorkoutPlan());
    }

    /// <summary>The active diet plan is found among drafts and completed plans.</summary>
    [TestMethod]
    public void GetActiveDietPlan_MixedStatuses_ReturnsActive()
    {
        var active = new DietPlan { Status = PlanStatus.Active };
        var client = new ClientProfile { DietPlans = { new DietPlan { Status = PlanStatus.Archived }, active } };

        Assert.AreSame(active, client.GetActiveDietPlan());
        Assert.IsNull(new ClientProfile().GetActiveDietPlan());
    }

    /// <summary>The latest measurement is the most recent by MeasuredAt, whatever the order loaded.</summary>
    [TestMethod]
    public void GetLatestMeasurement_UnorderedReadings_ReturnsMostRecent()
    {
        var latest = new HealthMeasurement { MeasuredAt = new DateTime(2026, 10, 20, 8, 0, 0), WeightKg = 80m, HeightCm = 180m };
        var client = new ClientProfile
        {
            Measurements =
            {
                new HealthMeasurement { MeasuredAt = new DateTime(2026, 10, 6, 8, 0, 0), WeightKg = 81m, HeightCm = 180m },
                latest,
                new HealthMeasurement { MeasuredAt = new DateTime(2026, 10, 13, 8, 0, 0), WeightKg = 80.5m, HeightCm = 180m }
            }
        };

        Assert.AreSame(latest, client.GetLatestMeasurement());
        Assert.IsNull(new ClientProfile().GetLatestMeasurement());
    }
}
