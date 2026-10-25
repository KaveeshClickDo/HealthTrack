using System.Globalization;
using HealthTrack.Models.Entities;
using HealthTrack.Models.Enums;
using HealthTrack.Tests.TestSupport;

namespace HealthTrack.Tests.Entities;

/// <summary>
/// Diet plans and their scheduled meals (BR-09, BR-10).
/// </summary>
[TestClass]
public sealed class DietPlanTests
{
    private static DietPlanMeal Meal(MealType type, string time, DayOfWeek? day = null) =>
        new() { MealType = type, ScheduledTime = TimeSpan.Parse(time, CultureInfo.InvariantCulture), DayOfWeek = day };

    private static DietPlan NewPlan(PlanStatus status = PlanStatus.Draft, DateTime? endDate = null) => new()
    {
        DailyCalorieTarget = 2000,
        StartDate = new DateTime(2026, 11, 1),
        EndDate = endDate,
        Status = status
    };

    /// <summary>An every-day meal applies to every weekday.</summary>
    [TestMethod]
    [DataRow(DayOfWeek.Sunday)]
    [DataRow(DayOfWeek.Wednesday)]
    [DataRow(DayOfWeek.Saturday)]
    public void AppliesTo_EveryDayMeal_AppliesToAnyDay(DayOfWeek day)
    {
        Assert.IsTrue(Meal(MealType.Breakfast, "07:30").AppliesTo(day));
    }

    /// <summary>A weekday meal applies to that day only.</summary>
    [TestMethod]
    [DataRow(DayOfWeek.Monday, true)]
    [DataRow(DayOfWeek.Tuesday, false)]
    [DataRow(DayOfWeek.Sunday, false)]
    public void AppliesTo_MondayMeal_AppliesToMondayOnly(DayOfWeek day, bool expected)
    {
        Assert.AreEqual(expected, Meal(MealType.Lunch, "12:30", DayOfWeek.Monday).AppliesTo(day));
    }

    /// <summary>A day's meals are the every-day ones plus that day's own, in time order.</summary>
    [TestMethod]
    public void GetMealsFor_Monday_ReturnsEveryDayAndMondayMealsByTime()
    {
        var plan = NewPlan();
        plan.Meals.Add(Meal(MealType.Lunch, "12:30", DayOfWeek.Monday));
        plan.Meals.Add(Meal(MealType.Dinner, "18:00", DayOfWeek.Tuesday));
        plan.Meals.Add(Meal(MealType.Breakfast, "07:30"));
        plan.Meals.Add(Meal(MealType.Snack, "10:00"));

        var meals = plan.GetMealsFor(DayOfWeek.Monday);

        CollectionAssert.AreEqual(new[] { MealType.Breakfast, MealType.Snack, MealType.Lunch }, meals.Select(m => m.MealType).ToArray());
    }

    /// <summary>An active plan applies from its start date to its end date, both included.</summary>
    [TestMethod]
    [DataRow("2026-10-31", false)]
    [DataRow("2026-11-01", true)]
    [DataRow("2026-11-15", true)]
    [DataRow("2026-11-30", true)]
    [DataRow("2026-12-01", false)]
    public void IsActiveOn_ActivePlanWithEndDate_TrueOnlyInsideRange(string date, bool expected)
    {
        var plan = NewPlan(PlanStatus.Active, new DateTime(2026, 11, 30));

        Assert.AreEqual(expected, plan.IsActiveOn(DateTime.Parse(date, CultureInfo.InvariantCulture)));
    }

    /// <summary>With no end date an active plan keeps applying.</summary>
    [TestMethod]
    public void IsActiveOn_NoEndDate_TrueLongAfterStart()
    {
        Assert.IsTrue(NewPlan(PlanStatus.Active).IsActiveOn(new DateTime(2027, 6, 1, 13, 0, 0)));
    }

    /// <summary>Only an Active plan applies, whatever the date.</summary>
    [TestMethod]
    [DataRow(PlanStatus.Draft)]
    [DataRow(PlanStatus.Completed)]
    [DataRow(PlanStatus.Archived)]
    public void IsActiveOn_NotActive_False(PlanStatus status)
    {
        Assert.IsFalse(NewPlan(status).IsActiveOn(new DateTime(2026, 11, 10)));
    }

    /// <summary>Draft → Active when the plan has a meal.</summary>
    [TestMethod]
    public void Activate_DraftWithMeal_BecomesActive()
    {
        var plan = NewPlan();
        plan.Meals.Add(Meal(MealType.Breakfast, "07:30"));

        plan.Activate();

        Assert.AreEqual(PlanStatus.Active, plan.Status);
    }

    /// <summary>BR-10: a plan needs at least one meal before activation.</summary>
    [TestMethod]
    public void Activate_NoMeals_ThrowsBr10()
    {
        RuleAssert.Broken("BR-10", NewPlan().Activate);
    }

    /// <summary>Only drafts can be activated.</summary>
    [TestMethod]
    public void Activate_AlreadyActive_ThrowsBr09()
    {
        var plan = NewPlan(PlanStatus.Active);
        plan.Meals.Add(Meal(MealType.Breakfast, "07:30"));

        RuleAssert.Broken("BR-09", plan.Activate);
    }

    /// <summary>BR-09: activating a newer plan completes this one.</summary>
    [TestMethod]
    public void Complete_Active_BecomesCompleted()
    {
        var plan = NewPlan(PlanStatus.Active);

        plan.Complete();

        Assert.AreEqual(PlanStatus.Completed, plan.Status);
    }

    /// <summary>Only an active plan can be completed.</summary>
    [TestMethod]
    public void Complete_Draft_ThrowsBr09()
    {
        RuleAssert.Broken("BR-09", NewPlan().Complete);
    }
}
