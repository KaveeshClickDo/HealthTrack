using HealthTrack.Models.Entities;
using HealthTrack.Models.Enums;
using HealthTrack.Models.ViewModels;
using HealthTrack.Services;
using HealthTrack.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Tests.Services;

/// <summary>
/// The exercise and food libraries (UC32): unique names, deactivation instead of deletion (BR-21), searching and pickers.
/// </summary>
[TestClass]
public sealed class LibraryServiceTests
{
    private static ExerciseViewModel ExerciseForm(string name, int? id = null) => new()
    {
        ExerciseId = id,
        Name = name,
        Category = ExerciseCategory.Strength,
        MuscleGroup = MuscleGroup.Legs,
        Equipment = " ",
        Instructions = "Keep your back straight.",
        MetValue = 5.0m
    };

    private static FoodItemViewModel FoodForm(string name) => new()
    {
        Name = name,
        Category = FoodCategory.Grains,
        ServingSize = 40m,
        ServingUnit = "G",
        CaloriesPerServing = 152m,
        ProteinG = 5.3m,
        CarbsG = 27.1m,
        FatG = 2.6m
    };

    private static async Task<Exercise> AddExerciseAsync(TestHost host, string name, bool isActive = true,
        ExerciseCategory category = ExerciseCategory.Cardio)
    {
        var exercise = new Exercise { Name = name, Category = category, MuscleGroup = MuscleGroup.FullBody, MetValue = 4m, IsActive = isActive };
        host.Db.Exercises.Add(exercise);
        await host.Db.SaveChangesAsync();
        return exercise;
    }

    /// <summary>A new exercise is saved active, blanks become null, and the change is audited.</summary>
    [TestMethod]
    public async Task SaveExerciseAsync_NewExercise_CreatesActiveAndAudits()
    {
        await using var host = await TestHost.CreateAsync();

        var id = await host.Get<ILibraryService>().SaveExerciseAsync(ExerciseForm(" Goblet squat "), "admin-id");

        var saved = await host.Db.Exercises.AsNoTracking().SingleAsync();
        Assert.AreEqual(saved.ExerciseId, id);
        Assert.AreEqual("Goblet squat", saved.Name);
        Assert.IsNull(saved.Equipment);
        Assert.IsTrue(saved.IsActive);
        var audit = await host.Db.AuditLogs.SingleAsync();
        Assert.AreEqual(AuditAction.Create, audit.Action);
        Assert.AreEqual("admin-id", audit.UserId);
    }

    /// <summary>Names are unique ignoring case and spaces, so a second "deadlift" is refused.</summary>
    [TestMethod]
    public async Task SaveExerciseAsync_DuplicateName_ThrowsBr21()
    {
        await using var host = await TestHost.CreateAsync();
        await AddExerciseAsync(host, "Deadlift");

        await RuleAssert.BrokenAsync("BR-21", () => host.Get<ILibraryService>().SaveExerciseAsync(ExerciseForm("  DEADLIFT "), "admin-id"));

        Assert.AreEqual(1, await host.Db.Exercises.CountAsync());
    }

    /// <summary>BR-21: a deactivated entry still holds its name, and the message says to reactivate it.</summary>
    [TestMethod]
    public async Task SaveExerciseAsync_NameOfInactiveExercise_SuggestsReactivating()
    {
        await using var host = await TestHost.CreateAsync();
        await AddExerciseAsync(host, "Deadlift", isActive: false);

        var exception = await RuleAssert.BrokenAsync("BR-21", () => host.Get<ILibraryService>().SaveExerciseAsync(ExerciseForm("Deadlift"), "admin-id"));

        StringAssert.Contains(exception.Message, "reactivate");
    }

    /// <summary>Editing an exercise may keep (or re-case) its own name.</summary>
    [TestMethod]
    public async Task SaveExerciseAsync_UpdateKeepingOwnName_Saves()
    {
        await using var host = await TestHost.CreateAsync();
        var exercise = await AddExerciseAsync(host, "Deadlift");

        var id = await host.Get<ILibraryService>().SaveExerciseAsync(ExerciseForm("deadlift", exercise.ExerciseId), "admin-id");

        Assert.AreEqual(exercise.ExerciseId, id);
        var saved = await host.Db.Exercises.AsNoTracking().SingleAsync();
        Assert.AreEqual("deadlift", saved.Name);
        Assert.AreEqual(5.0m, saved.MetValue);
        Assert.AreEqual(AuditAction.Update, (await host.Db.AuditLogs.SingleAsync()).Action);
    }

    /// <summary>Updating an exercise that doesn't exist answers null, so the controller can return 404.</summary>
    [TestMethod]
    public async Task SaveExerciseAsync_UnknownId_ReturnsNull()
    {
        await using var host = await TestHost.CreateAsync();

        Assert.IsNull(await host.Get<ILibraryService>().SaveExerciseAsync(ExerciseForm("Anything", 99), "admin-id"));
    }

    /// <summary>BR-21: a deactivated exercise leaves the pickers and the default list but stays in the database.</summary>
    [TestMethod]
    public async Task SetExerciseActiveAsync_Deactivate_HidesButKeeps()
    {
        await using var host = await TestHost.CreateAsync();
        var plank = await AddExerciseAsync(host, "Plank");
        await AddExerciseAsync(host, "Burpees");
        var library = host.Get<ILibraryService>();

        Assert.IsTrue(await library.SetExerciseActiveAsync(plank.ExerciseId, false, "admin-id"));

        CollectionAssert.AreEqual(new[] { "Burpees" }, (await library.GetActiveExercisesAsync()).Select(e => e.Name).ToArray());
        Assert.AreEqual(1, (await library.GetExercisesAsync(null, null, includeInactive: false, 1)).TotalCount);
        Assert.AreEqual(2, (await library.GetExercisesAsync(null, null, includeInactive: true, 1)).TotalCount);
        Assert.AreEqual(2, await host.Db.Exercises.CountAsync());
        Assert.AreEqual(1, await host.Db.AuditLogs.CountAsync());
        Assert.IsFalse(await library.SetExerciseActiveAsync(999, false, "admin-id"));
    }

    /// <summary>The list searches by part of the name, ignoring case, within a category.</summary>
    [TestMethod]
    public async Task GetExercisesAsync_SearchAndCategory_FiltersByName()
    {
        await using var host = await TestHost.CreateAsync();
        await AddExerciseAsync(host, "Running (8 km/h)");
        await AddExerciseAsync(host, "Running (10 km/h)");
        await AddExerciseAsync(host, "Barbell squat", category: ExerciseCategory.Strength);

        var result = await host.Get<ILibraryService>().GetExercisesAsync("RUN", ExerciseCategory.Cardio, false, 1);

        CollectionAssert.AreEqual(new[] { "Running (10 km/h)", "Running (8 km/h)" }, result.Items.Select(e => e.Name).ToArray());
    }

    /// <summary>Foods are unique by name too.</summary>
    [TestMethod]
    public async Task SaveFoodAsync_DuplicateName_ThrowsBr21()
    {
        await using var host = await TestHost.CreateAsync();
        var library = host.Get<ILibraryService>();
        await library.SaveFoodAsync(FoodForm("Rolled oats"), "admin-id");

        await RuleAssert.BrokenAsync("BR-21", () => library.SaveFoodAsync(FoodForm("rolled OATS"), "admin-id"));
    }

    /// <summary>A new food stores the unit in lower case and appears in the picker; deactivating removes it.</summary>
    [TestMethod]
    public async Task SaveFoodAsync_NewFood_SavedAndDeactivatable()
    {
        await using var host = await TestHost.CreateAsync();
        var library = host.Get<ILibraryService>();

        var id = await library.SaveFoodAsync(FoodForm("Rolled oats"), "admin-id");

        var saved = await host.Db.FoodItems.AsNoTracking().SingleAsync();
        Assert.AreEqual("g", saved.ServingUnit);
        Assert.AreEqual(152m, saved.CaloriesPerServing);
        Assert.HasCount(1, await library.GetActiveFoodsAsync());

        Assert.IsTrue(await library.SetFoodActiveAsync(id!.Value, false, "admin-id"));
        Assert.IsEmpty(await library.GetActiveFoodsAsync());
        Assert.AreEqual("Rolled oats", (await library.GetFoodAsync(id.Value))?.Name);
    }
}
