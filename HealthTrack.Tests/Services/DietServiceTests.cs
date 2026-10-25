using HealthTrack.Models.Entities;
using HealthTrack.Models.Enums;
using HealthTrack.Models.ViewModels;
using HealthTrack.Services;
using HealthTrack.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Tests.Services;

/// <summary>
/// Diet plans and meal logging (UC08, UC10, UC21, UC36; BR-08 to BR-10, BR-13, BR-14, BR-21). The test clock reads
/// Sunday 25 October 2026, 09:00.
/// </summary>
[TestClass]
public sealed class DietServiceTests
{
    private static async Task<(TestHost Host, TrainerProfile Trainer, ClientProfile Client)> ArrangeAsync()
    {
        var host = await TestHost.CreateAsync();
        var trainer = await host.CreateTrainerAsync("trainer@example.test", firstName: "Chris");
        var client = await host.CreateClientAsync("client@example.test");
        await host.AssignAsync(trainer, client);
        return (host, trainer, client);
    }

    private static DietPlanViewModel PlanForm(int clientId) => new()
    {
        ClientId = clientId,
        Title = " Lean autumn ",
        DailyCalorieTarget = 1900,
        ProteinTargetG = 120,
        StartDate = new DateTime(2026, 10, 26)
    };

    private static LogMealViewModel MealForm(TestHost host, params (FoodItem Food, decimal Servings)[] items) => new()
    {
        MealDate = host.Clock.Today,
        MealType = MealType.Breakfast,
        Items = items.Select(i => new LogMealItemViewModel { FoodItemId = i.Food.FoodItemId, Quantity = i.Servings }).ToList()
    };

    /// <summary>Spec 1.10: the form for an assigned client pre-fills the suggested target (80 kg, losing weight: 1,987).</summary>
    [TestMethod]
    public async Task GetCreatePlanFormAsync_AssignedClient_PrefillsSuggestedTarget()
    {
        var (host, trainer, client) = await ArrangeAsync();
        await using var _ = host;
        await host.AddMeasurementAsync(client, 80m);

        var form = await host.Get<IDietService>().GetCreatePlanFormAsync(trainer.UserId, client.ClientId);

        Assert.AreEqual(1987, form!.SuggestedCalorieTarget);
        Assert.AreEqual(1987, form.DailyCalorieTarget);
        Assert.AreEqual("Client User", form.ClientName);
        Assert.AreEqual(host.Clock.Today, form.StartDate);
    }

    /// <summary>A suggestion above the plan's 5,000 kcal limit is pre-filled as 5,000.</summary>
    [TestMethod]
    public async Task GetCreatePlanFormAsync_SuggestionAboveLimit_PrefillsWithin5000()
    {
        var (host, trainer, client) = await ArrangeAsync();
        await using var _ = host;
        client.FitnessGoal = FitnessGoal.GainMuscle;
        client.ActivityLevel = ActivityLevel.ExtraActive;
        await host.Db.SaveChangesAsync();
        var big = await host.AddMeasurementAsync(client, 200m);
        big.HeightCm = 200m;
        await host.Db.SaveChangesAsync();

        var form = await host.Get<IDietService>().GetCreatePlanFormAsync(trainer.UserId, client.ClientId);

        // (2,000 + 1,250 − 180 − 78) × 1.9 + 300 = 5,984.8.
        Assert.AreEqual(5985, form!.SuggestedCalorieTarget);
        Assert.AreEqual(5000, form.DailyCalorieTarget);
    }

    /// <summary>BR-08: no form, and no plan, for a client who isn't assigned to the trainer.</summary>
    [TestMethod]
    public async Task CreatePlanAsync_UnassignedClient_ReturnsNull()
    {
        var (host, _, client) = await ArrangeAsync();
        await using var __ = host;
        var stranger = await host.CreateTrainerAsync("stranger@example.test");
        var diet = host.Get<IDietService>();

        Assert.IsNull(await diet.GetCreatePlanFormAsync(stranger.UserId, client.ClientId));
        Assert.IsNull(await diet.CreatePlanAsync(stranger.UserId, PlanForm(client.ClientId)));
        Assert.IsFalse(await host.Db.DietPlans.AnyAsync());
    }

    /// <summary>A new plan is a Draft with the form's targets and dates, and is audited.</summary>
    [TestMethod]
    public async Task CreatePlanAsync_AssignedClient_CreatesDraft()
    {
        var (host, trainer, client) = await ArrangeAsync();
        await using var _ = host;

        var id = await host.Get<IDietService>().CreatePlanAsync(trainer.UserId, PlanForm(client.ClientId));

        var plan = await host.Db.DietPlans.AsNoTracking().SingleAsync();
        Assert.AreEqual(plan.DietPlanId, id);
        Assert.AreEqual("Lean autumn", plan.Title);
        Assert.AreEqual(PlanStatus.Draft, plan.Status);
        Assert.AreEqual(1900, plan.DailyCalorieTarget);
        Assert.AreEqual(120, plan.ProteinTargetG);
        Assert.AreEqual(AuditAction.Create, (await host.Db.AuditLogs.SingleAsync()).Action);
    }

    /// <summary>BR-10: the end date can't be before the start date.</summary>
    [TestMethod]
    public async Task CreatePlanAsync_EndBeforeStart_ThrowsBr10()
    {
        var (host, trainer, client) = await ArrangeAsync();
        await using var _ = host;
        var form = PlanForm(client.ClientId);
        form.EndDate = form.StartDate!.Value.AddDays(-1);

        await RuleAssert.BrokenAsync("BR-10", () => host.Get<IDietService>().CreatePlanAsync(trainer.UserId, form));
    }

    /// <summary>BR-09: activating completes the client's previous diet plan, and the client is told.</summary>
    [TestMethod]
    public async Task ActivatePlanAsync_PreviousActivePlan_CompletesItAndNotifies()
    {
        var (host, trainer, client) = await ArrangeAsync();
        await using var _ = host;
        var previous = await host.AddDietPlanAsync(trainer, client, PlanStatus.Active, meals: TestHost.Meal(MealType.Lunch));
        var next = await host.AddDietPlanAsync(trainer, client, PlanStatus.Draft, meals: TestHost.Meal(MealType.Dinner));

        Assert.IsTrue(await host.Get<IDietService>().ActivatePlanAsync(next.DietPlanId, trainer.UserId));

        var plans = await host.Db.DietPlans.AsNoTracking().ToDictionaryAsync(p => p.DietPlanId);
        Assert.AreEqual(PlanStatus.Completed, plans[previous.DietPlanId].Status);
        Assert.AreEqual(PlanStatus.Active, plans[next.DietPlanId].Status);
        var notification = await host.Db.Notifications.SingleAsync();
        Assert.AreEqual(client.UserId, notification.UserId);
        Assert.AreEqual(NotificationType.PlanAssigned, notification.Type);
        Assert.AreEqual("/Client/Diet/MyPlan", notification.LinkUrl);
    }

    /// <summary>BR-10: a plan without meals can't be activated; the current plan stays active.</summary>
    [TestMethod]
    public async Task ActivatePlanAsync_NoMeals_ThrowsBr10()
    {
        var (host, trainer, client) = await ArrangeAsync();
        await using var _ = host;
        var current = await host.AddDietPlanAsync(trainer, client, PlanStatus.Active, meals: TestHost.Meal(MealType.Lunch));
        var empty = await host.AddDietPlanAsync(trainer, client);

        await RuleAssert.BrokenAsync("BR-10", () => host.Get<IDietService>().ActivatePlanAsync(empty.DietPlanId, trainer.UserId));

        Assert.AreEqual(PlanStatus.Active, (await host.Db.DietPlans.AsNoTracking().SingleAsync(p => p.DietPlanId == current.DietPlanId)).Status);
        Assert.IsFalse(await host.Db.Notifications.AnyAsync());
    }

    /// <summary>Only the author reaches a plan, and only while the client is assigned (BR-08).</summary>
    [TestMethod]
    public async Task AddMealAsync_OtherTrainerOrEndedAssignment_ReturnsFalse()
    {
        var (host, trainer, client) = await ArrangeAsync();
        await using var _ = host;
        var other = await host.CreateTrainerAsync("other@example.test");
        var plan = await host.AddDietPlanAsync(trainer, client);
        var meal = new PlanMealViewModel { MealType = MealType.Snack, ScheduledTime = TimeSpan.FromHours(16), MealDescription = "Apple", TargetCalories = 100 };
        var diet = host.Get<IDietService>();

        Assert.IsFalse(await diet.AddMealAsync(plan.DietPlanId, other.UserId, meal));
        Assert.IsTrue(await diet.AddMealAsync(plan.DietPlanId, trainer.UserId, meal));

        var assignment = await host.Db.TrainerAssignments.SingleAsync();
        assignment.EndDate = host.Clock.Today;
        await host.Db.SaveChangesAsync();
        Assert.IsFalse(await diet.AddMealAsync(plan.DietPlanId, trainer.UserId, meal));
        Assert.IsNull(await diet.GetPlanForEditAsync(plan.DietPlanId, trainer.UserId));
    }

    /// <summary>BR-21: a meal the client has logged can't be removed; an unlogged one can.</summary>
    [TestMethod]
    public async Task UpdatePlanAsync_RemoveLoggedMeal_ThrowsBr21()
    {
        var (host, trainer, client) = await ArrangeAsync();
        await using var _ = host;
        var plan = await host.AddDietPlanAsync(trainer, client, PlanStatus.Active, 2000, null,
            TestHost.Meal(MealType.Breakfast, hour: 7), TestHost.Meal(MealType.Lunch, hour: 12));
        var breakfast = plan.Meals.Single(m => m.MealType == MealType.Breakfast);
        var lunch = plan.Meals.Single(m => m.MealType == MealType.Lunch);
        var oats = await host.AddFoodAsync("Oats", 389m);
        var log = new MealLog { ClientId = client.ClientId, DietPlanMealId = breakfast.DietPlanMealId, MealDate = host.Clock.Today, MealType = MealType.Breakfast, CreatedAt = host.Clock.Now };
        log.AddItem(oats, 1m);
        host.Db.MealLogs.Add(log);
        await host.Db.SaveChangesAsync();
        var diet = host.Get<IDietService>();

        DietPlanEditViewModel Form(bool removeBreakfast, bool removeLunch) => new()
        {
            Title = "Diet",
            DailyCalorieTarget = 2100,
            StartDate = plan.StartDate,
            Meals =
            [
                new PlanMealEditViewModel { DietPlanMealId = breakfast.DietPlanMealId, ScheduledTime = TimeSpan.FromHours(7), MealDescription = "Oats", TargetCalories = 450, Remove = removeBreakfast },
                new PlanMealEditViewModel { DietPlanMealId = lunch.DietPlanMealId, ScheduledTime = TimeSpan.FromHours(13), MealDescription = "Rice", TargetCalories = 700, Remove = removeLunch }
            ]
        };

        await RuleAssert.BrokenAsync("BR-21", () => diet.UpdatePlanAsync(plan.DietPlanId, trainer.UserId, Form(removeBreakfast: true, removeLunch: false)));
        host.Db.ChangeTracker.Clear();

        Assert.IsTrue(await diet.UpdatePlanAsync(plan.DietPlanId, trainer.UserId, Form(removeBreakfast: false, removeLunch: true)));
        var stored = await host.Db.DietPlans.AsNoTracking().Include(p => p.Meals).SingleAsync();
        Assert.AreEqual(2100, stored.DailyCalorieTarget);
        var kept = stored.Meals.Single();
        Assert.AreEqual(breakfast.DietPlanMealId, kept.DietPlanMealId);
        Assert.AreEqual(450, kept.TargetCalories);
    }

    /// <summary>BR-10: removing the last meal of an active plan is refused.</summary>
    [TestMethod]
    public async Task UpdatePlanAsync_RemoveLastMealOfActivePlan_ThrowsBr10()
    {
        var (host, trainer, client) = await ArrangeAsync();
        await using var _ = host;
        var plan = await host.AddDietPlanAsync(trainer, client, PlanStatus.Active, meals: TestHost.Meal(MealType.Lunch));

        await RuleAssert.BrokenAsync("BR-10", () => host.Get<IDietService>().UpdatePlanAsync(plan.DietPlanId, trainer.UserId, new DietPlanEditViewModel
        {
            Title = "Diet",
            DailyCalorieTarget = 2000,
            StartDate = plan.StartDate,
            Meals = [new PlanMealEditViewModel { DietPlanMealId = plan.Meals.Single().DietPlanMealId, Remove = true }]
        }));
    }

    /// <summary>
    /// UC36, BR-14: each item stores quantity × the per-serving values; later library changes don't rewrite it. The
    /// audit row holds no nutrition values.
    /// </summary>
    [TestMethod]
    public async Task LogMealAsync_Foods_StoresNutritionSnapshots()
    {
        var (host, _, client) = await ArrangeAsync();
        await using var __ = host;
        var oats = await host.AddFoodAsync("Oats", 389m, 16.9m, 66.3m, 6.9m);
        var banana = await host.AddFoodAsync("Banana", 105m, 1.3m, 27m, 0.4m);

        var id = await host.Get<IDietService>().LogMealAsync(client.UserId, MealForm(host, (oats, 0.5m), (banana, 2m)));

        oats.CaloriesPerServing = 999m;
        await host.Db.SaveChangesAsync();
        var log = await host.Db.MealLogs.AsNoTracking().Include(m => m.Items).SingleAsync();
        Assert.AreEqual(log.MealLogId, id);
        Assert.IsNull(log.DietPlanMealId);
        var oatsItem = log.Items.Single(i => i.FoodItemId == oats.FoodItemId);
        Assert.AreEqual(194.50m, oatsItem.Calories);
        Assert.AreEqual(8.45m, oatsItem.ProteinG);
        Assert.AreEqual(33.15m, oatsItem.CarbsG);
        Assert.AreEqual(3.45m, oatsItem.FatG);
        Assert.AreEqual(210m, log.Items.Single(i => i.FoodItemId == banana.FoodItemId).Calories);
        Assert.AreEqual(404.50m, log.TotalCalories);

        var audit = await host.Db.AuditLogs.SingleAsync(a => a.EntityName == nameof(MealLog));
        Assert.IsFalse(audit.Details!.Contains("194") || audit.Details.Contains("404"));
    }

    /// <summary>BR-13: a meal can't be logged for a future date.</summary>
    [TestMethod]
    public async Task LogMealAsync_FutureDate_ThrowsBr13()
    {
        var (host, _, client) = await ArrangeAsync();
        await using var __ = host;
        var oats = await host.AddFoodAsync("Oats", 389m);
        var form = MealForm(host, (oats, 1m));
        form.MealDate = host.Clock.Today.AddDays(1);

        await RuleAssert.BrokenAsync("BR-13", () => host.Get<IDietService>().LogMealAsync(client.UserId, form));
        Assert.IsFalse(await host.Db.MealLogs.AnyAsync());
    }

    /// <summary>BR-13: a meal log needs at least one food.</summary>
    [TestMethod]
    public async Task LogMealAsync_NoFoods_ThrowsBr13()
    {
        var (host, _, client) = await ArrangeAsync();
        await using var __ = host;
        var form = MealForm(host);
        form.Items = [new LogMealItemViewModel { FoodItemId = null, Quantity = 1m }];

        await RuleAssert.BrokenAsync("BR-13", () => host.Get<IDietService>().LogMealAsync(client.UserId, form));
        Assert.IsFalse(await host.Db.MealLogs.AnyAsync());
    }

    /// <summary>BR-21: a deactivated food can't be logged.</summary>
    [TestMethod]
    public async Task LogMealAsync_InactiveFood_ThrowsBr21()
    {
        var (host, _, client) = await ArrangeAsync();
        await using var __ = host;
        var oats = await host.AddFoodAsync("Oats", 389m);
        var retired = await host.AddFoodAsync("Retired bar", 250m, isActive: false);

        await RuleAssert.BrokenAsync("BR-21", () => host.Get<IDietService>().LogMealAsync(client.UserId, MealForm(host, (oats, 1m), (retired, 1m))));
        Assert.IsFalse(await host.Db.MealLogs.AnyAsync());
    }

    /// <summary>A planned meal is linked to the log; another client's planned meal answers "not found".</summary>
    [TestMethod]
    public async Task LogMealAsync_PlannedMeal_LinksOnlyTheClientsOwn()
    {
        var (host, trainer, client) = await ArrangeAsync();
        await using var _ = host;
        var other = await host.CreateClientAsync("other@example.test");
        var own = await host.AddDietPlanAsync(trainer, client, PlanStatus.Active, meals: TestHost.Meal(MealType.Breakfast));
        var others = await host.AddDietPlanAsync(trainer, other, PlanStatus.Active, meals: TestHost.Meal(MealType.Breakfast));
        var oats = await host.AddFoodAsync("Oats", 389m);
        var diet = host.Get<IDietService>();

        var form = MealForm(host, (oats, 1m));
        form.DietPlanMealId = others.Meals.Single().DietPlanMealId;
        Assert.IsNull(await diet.LogMealAsync(client.UserId, form));
        Assert.IsNull(await diet.GetLogMealFormAsync(client.ClientId, others.Meals.Single().DietPlanMealId));

        form.DietPlanMealId = own.Meals.Single().DietPlanMealId;
        Assert.IsNotNull(await diet.LogMealAsync(client.UserId, form));
        Assert.AreEqual(own.Meals.Single().DietPlanMealId, (await host.Db.MealLogs.AsNoTracking().SingleAsync()).DietPlanMealId);
    }

    /// <summary>The log form for a planned meal pre-selects its meal type and shows what was planned.</summary>
    [TestMethod]
    public async Task GetLogMealFormAsync_PlannedMeal_PrefillsTypeAndPlan()
    {
        var (host, trainer, client) = await ArrangeAsync();
        await using var _ = host;
        var plan = await host.AddDietPlanAsync(trainer, client, PlanStatus.Active, meals: TestHost.Meal(MealType.Dinner, kcal: 650));
        await host.AddFoodAsync("Oats", 389m);
        await host.AddFoodAsync("Retired bar", 250m, isActive: false);

        var form = await host.Get<IDietService>().GetLogMealFormAsync(client.ClientId, plan.Meals.Single().DietPlanMealId);

        Assert.AreEqual(MealType.Dinner, form!.MealType);
        Assert.AreEqual("Dinner meal", form.PlannedDescription);
        Assert.AreEqual(650, form.PlannedCalories);
        Assert.AreEqual(host.Clock.Today, form.MealDate);
        Assert.AreEqual("Oats", form.FoodOptions.Single().Name);
        Assert.HasCount(1, form.Items);
    }

    /// <summary>Today (a Sunday) has the every-day meals and Sunday's own, by time, each marked logged or not.</summary>
    [TestMethod]
    public async Task GetTodaysMealsAsync_ActivePlan_MarksLoggedMeals()
    {
        var (host, trainer, client) = await ArrangeAsync();
        await using var _ = host;
        var plan = await host.AddDietPlanAsync(trainer, client, PlanStatus.Active, 2000, null,
            TestHost.Meal(MealType.Dinner, hour: 19),
            TestHost.Meal(MealType.Breakfast, DayOfWeek.Sunday, hour: 9),
            TestHost.Meal(MealType.Lunch, DayOfWeek.Monday, hour: 12));
        var oats = await host.AddFoodAsync("Oats", 389m);
        var breakfast = plan.Meals.Single(m => m.MealType == MealType.Breakfast);
        await host.Get<IDietService>().LogMealAsync(client.UserId, new LogMealViewModel
        {
            DietPlanMealId = breakfast.DietPlanMealId,
            MealDate = host.Clock.Today,
            MealType = MealType.Breakfast,
            Items = [new LogMealItemViewModel { FoodItemId = oats.FoodItemId, Quantity = 1m }]
        });

        var meals = await host.Get<IDietService>().GetTodaysMealsAsync(client.ClientId, host.Clock.Today);

        CollectionAssert.AreEqual(new[] { MealType.Breakfast, MealType.Dinner }, meals.Select(m => m.MealType).ToArray());
        Assert.IsTrue(meals[0].IsLogged);
        Assert.AreEqual(389m, meals[0].LoggedCalories);
        Assert.IsFalse(meals[1].IsLogged);
    }

    /// <summary>No meals today before the plan's start date.</summary>
    [TestMethod]
    public async Task GetTodaysMealsAsync_PlanNotStarted_ReturnsNone()
    {
        var (host, trainer, client) = await ArrangeAsync();
        await using var _ = host;
        await host.AddDietPlanAsync(trainer, client, PlanStatus.Active, 2000, host.Clock.Today.AddDays(1), TestHost.Meal(MealType.Lunch));

        Assert.IsEmpty(await host.Get<IDietService>().GetTodaysMealsAsync(client.ClientId, host.Clock.Today));
        Assert.IsFalse((await host.Get<IDietService>().GetWeekAsync(client.ClientId)).HasStarted);
    }

    /// <summary>The day's totals are the sum of its items, against the targets of the plan in effect that day.</summary>
    [TestMethod]
    public async Task GetDailySummaryAsync_LoggedMeals_TotalsAgainstTarget()
    {
        var (host, trainer, client) = await ArrangeAsync();
        await using var _ = host;
        var plan = await host.AddDietPlanAsync(trainer, client, PlanStatus.Active, 2000, meals: TestHost.Meal(MealType.Lunch));
        plan.ProteinTargetG = 130;
        await host.Db.SaveChangesAsync();
        var oats = await host.AddFoodAsync("Oats", 389m, 16.9m);
        var rice = await host.AddFoodAsync("Rice", 130m, 2.7m);
        var diet = host.Get<IDietService>();
        await diet.LogMealAsync(client.UserId, MealForm(host, (oats, 1m)));
        var lunch = MealForm(host, (rice, 3m));
        lunch.MealType = MealType.Lunch;
        await diet.LogMealAsync(client.UserId, lunch);

        var day = await diet.GetDailySummaryAsync(client.ClientId, host.Clock.Today);

        Assert.AreEqual(779m, day.CaloriesEaten);
        Assert.AreEqual(25m, day.ProteinG);
        Assert.AreEqual(2000, day.CalorieTarget);
        Assert.AreEqual(130, day.ProteinTargetG);
        Assert.AreEqual(39, day.PercentOfTarget);
        Assert.HasCount(2, day.Meals);
    }

    /// <summary>History lists the days with meals, newest first; a day before any plan has no target.</summary>
    [TestMethod]
    public async Task GetHistoryAsync_SeveralDays_NewestFirstWithTargets()
    {
        var (host, trainer, client) = await ArrangeAsync();
        await using var _ = host;
        await host.AddDietPlanAsync(trainer, client, PlanStatus.Active, 1800, host.Clock.Today.AddDays(-2), TestHost.Meal(MealType.Lunch));
        var rice = await host.AddFoodAsync("Rice", 130m);
        var diet = host.Get<IDietService>();
        foreach (var daysAgo in new[] { 5, 0, 1 })
        {
            var form = MealForm(host, (rice, 2m));
            form.MealDate = host.Clock.Today.AddDays(-daysAgo);
            await diet.LogMealAsync(client.UserId, form);
        }

        var history = await diet.GetHistoryAsync(client.ClientId, 1);

        CollectionAssert.AreEqual(new[] { host.Clock.Today, host.Clock.Today.AddDays(-1), host.Clock.Today.AddDays(-5) },
            history.Items.Select(d => d.Date).ToArray());
        Assert.AreEqual(1800, history.Items[0].CalorieTarget);
        Assert.IsNull(history.Items[2].CalorieTarget);
        Assert.AreEqual(260m, history.Items[2].CaloriesEaten);
        Assert.AreEqual(3, history.TotalCount);
    }
}
