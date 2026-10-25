using System.ComponentModel.DataAnnotations;
using HealthTrack.Models.ViewModels;

namespace HealthTrack.Tests.ViewModels;

/// <summary>
/// The workout log form's own rule (BR-13) and how its checkboxes bind.
/// </summary>
[TestClass]
public sealed class LogWorkoutViewModelTests
{
    /// <summary>
    /// An unticked checkbox posts nothing, so binding leaves Include at its default: the default must be "not done",
    /// or unticking a row would have no effect.
    /// </summary>
    [TestMethod]
    public void Include_NotPosted_DefaultsToNotDone()
    {
        Assert.IsFalse(new LogEntryViewModel().Include);
    }

    /// <summary>BR-13: a form whose rows are all unticked fails with the "tick at least one" message.</summary>
    [TestMethod]
    public void Validate_NoTickedRows_ReturnsNoEntriesError()
    {
        var model = new LogWorkoutViewModel
        {
            WorkoutDate = new DateTime(2026, 10, 25),
            Entries = [new LogEntryViewModel { ExerciseId = 1, DurationMinutes = 30 }]
        };

        var errors = model.Validate(new ValidationContext(model)).ToList();

        Assert.AreEqual(LogWorkoutViewModel.NoEntriesMessage, errors.Single().ErrorMessage);
    }

    /// <summary>One ticked row is enough.</summary>
    [TestMethod]
    public void Validate_OneTickedRow_IsValid()
    {
        var model = new LogWorkoutViewModel
        {
            WorkoutDate = new DateTime(2026, 10, 25),
            Entries = [new LogEntryViewModel { ExerciseId = 1, DurationMinutes = 30 }, new LogEntryViewModel { Include = true, ExerciseId = 2, DurationMinutes = 10 }]
        };

        Assert.IsEmpty(model.Validate(new ValidationContext(model)));
    }
}
