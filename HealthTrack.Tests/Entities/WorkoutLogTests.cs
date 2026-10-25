using HealthTrack.Models.Entities;
using HealthTrack.Models.Enums;

namespace HealthTrack.Tests.Entities;

/// <summary>
/// Workout logging: calorie snapshots (BR-14) and session totals.
/// </summary>
[TestClass]
public sealed class WorkoutLogTests
{
    private static Exercise NewExercise(int id, decimal met) =>
        new() { ExerciseId = id, Name = $"Exercise {id}", Category = ExerciseCategory.Cardio, MuscleGroup = MuscleGroup.Legs, MetValue = met };

    /// <summary>MET 8.0 × 70 kg × 30/60 h = 280 kcal.</summary>
    [TestMethod]
    public void AddEntry_Exercise_StoresMetTimesWeightTimesHours()
    {
        var log = new WorkoutLog();

        var entry = log.AddEntry(NewExercise(4, 8.0m), weightKg: 70m, minutes: 30);

        Assert.AreEqual(280.00m, entry.CaloriesBurned);
        Assert.AreEqual(4, entry.ExerciseId);
        Assert.AreEqual(30, entry.DurationMinutes);
        Assert.AreSame(entry, log.Entries.Single());
    }

    /// <summary>Sets, reps, load and distance are stored on the entry.</summary>
    [TestMethod]
    public void AddEntry_WithDetails_StoresThem()
    {
        var entry = new WorkoutLog().AddEntry(NewExercise(10, 5.0m), 80m, 20, sets: 4, reps: 8, loadKg: 60m, distanceKm: 1.5m);

        Assert.AreEqual(4, entry.SetsCompleted);
        Assert.AreEqual(8, entry.RepsCompleted);
        Assert.AreEqual(60m, entry.WeightKg);
        Assert.AreEqual(1.5m, entry.DistanceKm);
    }

    /// <summary>The calories are a snapshot: changing the library's MET later doesn't change history.</summary>
    [TestMethod]
    public void AddEntry_MetChangedLater_KeepsSnapshot()
    {
        var exercise = NewExercise(4, 8.0m);
        var entry = new WorkoutLog().AddEntry(exercise, 70m, 30);

        exercise.MetValue = 12.0m;

        Assert.AreEqual(280.00m, entry.CaloriesBurned);
    }

    /// <summary>A zero-minute entry is refused.</summary>
    [TestMethod]
    public void AddEntry_ZeroMinutes_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new WorkoutLog().AddEntry(NewExercise(4, 8.0m), 70m, 0));
    }

    /// <summary>Session totals are sums of the entries.</summary>
    [TestMethod]
    public void Totals_TwoEntries_SumMinutesAndCalories()
    {
        var log = new WorkoutLog();
        log.AddEntry(NewExercise(1, 8.0m), 70m, 30);
        log.AddEntry(NewExercise(2, 3.5m), 70m, 20);

        Assert.AreEqual(50, log.TotalDurationMinutes);
        Assert.AreEqual(361.67m, log.TotalCaloriesBurned);
    }

    /// <summary>EstimateCalories uses the same formula.</summary>
    [TestMethod]
    public void EstimateCalories_Exercise_UsesMetFormula()
    {
        Assert.AreEqual(216.30m, NewExercise(1, 3.5m).EstimateCalories(82.4m, 45));
    }
}
