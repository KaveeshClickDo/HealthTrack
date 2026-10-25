using HealthTrack.Models.Entities;

namespace HealthTrack.Tests.Entities;

/// <summary>
/// BR-11: every plan exercise has sets and reps, or a duration.
/// </summary>
[TestClass]
public sealed class WorkoutPlanExerciseTests
{
    /// <summary>Sets with reps, or a duration, is a valid prescription; anything less is not.</summary>
    [TestMethod]
    [DataRow(3, 10, null, true)]
    [DataRow(null, null, 30, true)]
    [DataRow(3, 10, 20, true)]
    [DataRow(3, null, null, false)]
    [DataRow(null, 10, null, false)]
    [DataRow(null, null, null, false)]
    [DataRow(0, 10, null, false)]
    [DataRow(null, null, 0, false)]
    [DataRow(3, null, 15, true)]
    public void IsValidPrescription_SetsRepsAndDuration_FollowsBr11(int? sets, int? reps, int? durationMinutes, bool expected)
    {
        var line = new WorkoutPlanExercise { Sets = sets, Reps = reps, DurationMinutes = durationMinutes };

        Assert.AreEqual(expected, line.IsValidPrescription());
    }
}
