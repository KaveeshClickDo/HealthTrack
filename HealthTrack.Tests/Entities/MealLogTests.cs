using HealthTrack.Models.Entities;
using HealthTrack.Models.Enums;

namespace HealthTrack.Tests.Entities;

/// <summary>
/// Meal logging: nutrition snapshots of quantity × per-serving values (spec 1.10, BR-14) and meal totals.
/// </summary>
[TestClass]
public sealed class MealLogTests
{
    private static FoodItem Oats() => new()
    {
        FoodItemId = 1,
        Name = "Rolled oats",
        Category = FoodCategory.Grains,
        ServingSize = 40m,
        ServingUnit = "g",
        CaloriesPerServing = 152m,
        ProteinG = 5.3m,
        CarbsG = 27.1m,
        FatG = 2.6m
    };

    private static FoodItem Banana() => new()
    {
        FoodItemId = 2,
        Name = "Banana",
        Category = FoodCategory.Fruits,
        ServingSize = 1m,
        ServingUnit = "piece",
        CaloriesPerServing = 105m,
        ProteinG = 1.3m,
        CarbsG = 27.0m,
        FatG = 0.4m
    };

    /// <summary>1.5 servings store 1.5 × each per-serving value.</summary>
    [TestMethod]
    public void AddItem_OneAndAHalfServings_SnapshotsQuantityTimesPerServing()
    {
        var log = new MealLog();

        var item = log.AddItem(Oats(), 1.5m);

        Assert.AreEqual(1, item.FoodItemId);
        Assert.AreEqual(1.5m, item.Quantity);
        Assert.AreEqual(228.00m, item.Calories);
        Assert.AreEqual(7.95m, item.ProteinG);
        Assert.AreEqual(40.65m, item.CarbsG);
        Assert.AreEqual(3.90m, item.FatG);
        Assert.AreSame(item, log.Items.Single());
    }

    /// <summary>The nutrition is a snapshot: editing the food library later doesn't change history.</summary>
    [TestMethod]
    public void AddItem_FoodChangedLater_KeepsSnapshot()
    {
        var oats = Oats();
        var item = new MealLog().AddItem(oats, 1m);

        oats.CaloriesPerServing = 999m;
        oats.ProteinG = 50m;

        Assert.AreEqual(152m, item.Calories);
        Assert.AreEqual(5.3m, item.ProteinG);
    }

    /// <summary>A zero quantity is refused.</summary>
    [TestMethod]
    public void AddItem_ZeroQuantity_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new MealLog().AddItem(Oats(), 0m));
    }

    /// <summary>Meal totals are sums of the items.</summary>
    [TestMethod]
    public void Totals_TwoItems_SumCaloriesAndMacros()
    {
        var log = new MealLog();
        log.AddItem(Oats(), 1.5m);
        log.AddItem(Banana(), 1m);

        Assert.AreEqual(333.00m, log.TotalCalories);
        Assert.AreEqual(9.25m, log.TotalProteinG);
        Assert.AreEqual(67.65m, log.TotalCarbsG);
        Assert.AreEqual(4.30m, log.TotalFatG);
    }

    /// <summary>CaloriesFor is quantity × kcal per serving.</summary>
    [TestMethod]
    [DataRow(0.25, 38.0)]
    [DataRow(1.0, 152.0)]
    [DataRow(2.5, 380.0)]
    public void CaloriesFor_Quantity_MultipliesPerServing(double quantity, double expected)
    {
        Assert.AreEqual((decimal)expected, Oats().CaloriesFor((decimal)quantity));
    }
}
