using HealthTrack.Models.Entities;
using HealthTrack.Tests.TestSupport;

namespace HealthTrack.Tests.Entities;

/// <summary>
/// Ending an assignment keeps it as history (BR-06).
/// </summary>
[TestClass]
public sealed class TrainerAssignmentTests
{
    private static TrainerAssignment NewAssignment() => new() { StartDate = new DateTime(2026, 9, 1) };

    /// <summary>Ending sets a date-only EndDate and makes the assignment inactive.</summary>
    [TestMethod]
    public void End_Active_SetsEndDateAndIsNoLongerActive()
    {
        var assignment = NewAssignment();
        Assert.IsTrue(assignment.IsActive);

        assignment.End(new DateTime(2026, 10, 25, 14, 30, 0));

        Assert.AreEqual(new DateTime(2026, 10, 25), assignment.EndDate);
        Assert.IsFalse(assignment.IsActive);
    }

    /// <summary>An assignment may end on the day it started.</summary>
    [TestMethod]
    public void End_SameDayAsStart_IsAllowed()
    {
        var assignment = NewAssignment();

        assignment.End(new DateTime(2026, 9, 1));

        Assert.AreEqual(new DateTime(2026, 9, 1), assignment.EndDate);
    }

    /// <summary>An ended assignment can't be ended again.</summary>
    [TestMethod]
    public void End_AlreadyEnded_ThrowsBr06()
    {
        var assignment = NewAssignment();
        assignment.End(new DateTime(2026, 10, 1));

        RuleAssert.Broken("BR-06", () => assignment.End(new DateTime(2026, 10, 25)));

        Assert.AreEqual(new DateTime(2026, 10, 1), assignment.EndDate);
    }

    /// <summary>The end date can't be before the start date.</summary>
    [TestMethod]
    public void End_BeforeStart_ThrowsBr06()
    {
        RuleAssert.Broken("BR-06", () => NewAssignment().End(new DateTime(2026, 8, 31)));
    }
}
