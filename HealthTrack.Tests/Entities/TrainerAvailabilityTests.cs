using HealthTrack.Models.Entities;

namespace HealthTrack.Tests.Entities;

/// <summary>
/// Availability windows must cover the whole appointment (BR-15).
/// </summary>
[TestClass]
public sealed class TrainerAvailabilityTests
{
    // Monday 2 November 2026.
    private static readonly DateTime Monday = new(2026, 11, 2);

    private static TrainerAvailability MondayMorning() =>
        new() { DayOfWeek = DayOfWeek.Monday, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(12, 0, 0) };

    /// <summary>The window's weekday is the one the test dates rely on.</summary>
    [TestMethod]
    public void TestDate_IsAMonday()
    {
        Assert.AreEqual(DayOfWeek.Monday, Monday.DayOfWeek);
    }

    /// <summary>Starting at the opening time and ending exactly at the closing time both count as inside.</summary>
    [TestMethod]
    [DataRow(9, 0, 60, true)]
    [DataRow(11, 0, 60, true)]
    [DataRow(10, 15, 90, true)]
    [DataRow(11, 30, 60, false)]
    [DataRow(8, 45, 30, false)]
    [DataRow(12, 0, 30, false)]
    [DataRow(9, 0, 0, false)]
    public void Covers_MondayAppointment_FitsOnlyInsideWindow(int hour, int minute, int minutes, bool expected)
    {
        Assert.AreEqual(expected, MondayMorning().Covers(Monday.AddHours(hour).AddMinutes(minute), minutes));
    }

    /// <summary>A window on Monday never covers another weekday.</summary>
    [TestMethod]
    public void Covers_OtherWeekday_False()
    {
        Assert.IsFalse(MondayMorning().Covers(Monday.AddDays(1).AddHours(10), 30));
    }
}
