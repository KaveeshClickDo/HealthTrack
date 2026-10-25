using HealthTrack.Models.Entities;

namespace HealthTrack.Tests.Entities;

/// <summary>
/// Trainer approval, capacity (BR-07) and availability (BR-15).
/// </summary>
[TestClass]
public sealed class TrainerProfileTests
{
    private static TrainerAssignment Active() => new() { StartDate = new DateTime(2026, 9, 1) };

    private static TrainerAssignment Ended() => new() { StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 6, 30) };

    /// <summary>A trainer is approved once ApprovedAt has a value.</summary>
    [TestMethod]
    public void IsApproved_ApprovedAtSet_True()
    {
        Assert.IsFalse(new TrainerProfile().IsApproved);
        Assert.IsTrue(new TrainerProfile { ApprovedAt = new DateTime(2026, 10, 1) }.IsApproved);
    }

    /// <summary>The default capacity is 20 clients.</summary>
    [TestMethod]
    public void MaxClients_NewProfile_Is20()
    {
        Assert.AreEqual(20, new TrainerProfile().MaxClients);
    }

    /// <summary>Only assignments without an end date count.</summary>
    [TestMethod]
    public void ActiveClientCount_MixedAssignments_CountsOnlyActive()
    {
        var trainer = new TrainerProfile { Assignments = { Active(), Ended(), Active(), Ended() } };

        Assert.AreEqual(2, trainer.ActiveClientCount());
    }

    /// <summary>BR-07: room for a client while active clients are below MaxClients; ended ones don't count.</summary>
    [TestMethod]
    public void HasCapacity_BelowMax_True()
    {
        var trainer = new TrainerProfile { MaxClients = 2, Assignments = { Active(), Ended(), Ended() } };

        Assert.IsTrue(trainer.HasCapacity());
    }

    /// <summary>BR-07: a full trainer has no capacity.</summary>
    [TestMethod]
    public void HasCapacity_AtMax_False()
    {
        var trainer = new TrainerProfile { MaxClients = 2, Assignments = { Active(), Active() } };

        Assert.IsFalse(trainer.HasCapacity());
    }

    /// <summary>Available when any window covers the appointment.</summary>
    [TestMethod]
    public void IsAvailable_OneWindowCovers_True()
    {
        var trainer = new TrainerProfile
        {
            Availabilities =
            {
                new TrainerAvailability { DayOfWeek = DayOfWeek.Monday, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(12, 0, 0) },
                new TrainerAvailability { DayOfWeek = DayOfWeek.Monday, StartTime = new TimeSpan(14, 0, 0), EndTime = new TimeSpan(17, 0, 0) }
            }
        };

        Assert.IsTrue(trainer.IsAvailable(new DateTime(2026, 11, 2, 15, 0, 0), 60));
        Assert.IsFalse(trainer.IsAvailable(new DateTime(2026, 11, 2, 11, 30, 0), 60));
        Assert.IsFalse(trainer.IsAvailable(new DateTime(2026, 11, 3, 10, 0, 0), 60));
    }
}
