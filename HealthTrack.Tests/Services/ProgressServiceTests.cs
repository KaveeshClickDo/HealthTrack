using HealthTrack.Models.Entities;
using HealthTrack.Models.Enums;
using HealthTrack.Models.ViewModels;
using HealthTrack.Services;
using HealthTrack.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Tests.Services;

/// <summary>
/// Measurements, trends, adherence and the suggested calorie target (UC11, UC12, UC18, spec 1.10). The test clock reads
/// Sunday 25 October 2026, 09:00; test clients were born on 1 January 1990 (age 36) with gender Other.
/// </summary>
[TestClass]
public sealed class ProgressServiceTests
{
    private static readonly DateTime Monday12Oct = new(2026, 10, 12);

    private static WorkoutPlanExercise Line(Exercise exercise, DayOfWeek day) =>
        new() { ExerciseId = exercise.ExerciseId, DayOfWeek = day, SortOrder = 1, DurationMinutes = 30 };

    private static async Task AddLogAsync(TestHost host, ClientProfile client, DateTime date, params (Exercise Exercise, decimal Kcal)[] entries)
    {
        var log = new WorkoutLog { ClientId = client.ClientId, WorkoutDate = date, CreatedAt = host.Clock.Now };
        foreach (var (exercise, kcal) in entries)
        {
            log.Entries.Add(new WorkoutLogEntry { ExerciseId = exercise.ExerciseId, DurationMinutes = 30, CaloriesBurned = kcal });
        }

        host.Db.WorkoutLogs.Add(log);
        await host.Db.SaveChangesAsync();
    }

    private static MeasurementViewModel Form(TestHost host, decimal weight, decimal height) =>
        new() { MeasuredAt = host.Clock.Now.AddHours(-1), WeightKg = weight, HeightCm = height, Notes = "Felt bloated" };

    /// <summary>
    /// Spec 1.10 with the latest reading (80 kg, 170 cm), age 36, gender Other, moderately active: BMR 1,604.5,
    /// TDEE 2,486.975; −500 to lose weight, +300 to gain muscle, otherwise the TDEE.
    /// </summary>
    [TestMethod]
    [DataRow(FitnessGoal.LoseWeight, 1987)]
    [DataRow(FitnessGoal.GainMuscle, 2787)]
    [DataRow(FitnessGoal.MaintainWeight, 2487)]
    [DataRow(FitnessGoal.ImproveEndurance, 2487)]
    [DataRow(FitnessGoal.ImproveFlexibility, 2487)]
    [DataRow(FitnessGoal.GeneralWellbeing, 2487)]
    public async Task SuggestCalorieTargetAsync_EachGoal_AdjustsTdee(FitnessGoal goal, int expected)
    {
        await using var host = await TestHost.CreateAsync();
        var client = await host.CreateClientAsync("client@example.test");
        client.FitnessGoal = goal;
        await host.Db.SaveChangesAsync();
        await host.AddMeasurementAsync(client, 100m, host.Clock.Now.AddDays(-30));
        await host.AddMeasurementAsync(client, 80m, host.Clock.Now.AddDays(-1));

        Assert.AreEqual(expected, await host.Get<IProgressService>().SuggestCalorieTargetAsync(client.ClientId));
    }

    /// <summary>
    /// The gender constant (spec 1.10), losing weight at 80 kg: men (1,687.5 × 1.55 − 500 = 2,115.6), women
    /// (1,521.5 × 1.55 − 500 = 1,858.3).
    /// </summary>
    [TestMethod]
    [DataRow(Gender.Male, 2116)]
    [DataRow(Gender.Female, 1858)]
    public async Task SuggestCalorieTargetAsync_Gender_UsesItsConstant(Gender gender, int expected)
    {
        await using var host = await TestHost.CreateAsync();
        var client = await host.CreateClientAsync("client@example.test");
        client.User.Gender = gender;
        await host.Db.SaveChangesAsync();
        await host.AddMeasurementAsync(client, 80m);

        Assert.AreEqual(expected, await host.Get<IProgressService>().SuggestCalorieTargetAsync(client.ClientId));
    }

    /// <summary>The target never drops below 1,200 kcal, however small the TDEE.</summary>
    [TestMethod]
    public async Task SuggestCalorieTargetAsync_SmallSedentaryClient_FloorsAt1200()
    {
        await using var host = await TestHost.CreateAsync();
        var client = await host.CreateClientAsync("client@example.test");
        client.ActivityLevel = ActivityLevel.Sedentary;
        await host.Db.SaveChangesAsync();
        // 40 kg: BMR 1,204.5 × 1.2 = 1,445.4; − 500 = 945.4, raised to 1,200.
        await host.AddMeasurementAsync(client, 40m);

        Assert.AreEqual(1200, await host.Get<IProgressService>().SuggestCalorieTargetAsync(client.ClientId));
    }

    /// <summary>Without a measurement there is no weight or height to work from.</summary>
    [TestMethod]
    public async Task SuggestCalorieTargetAsync_NoMeasurement_ReturnsNull()
    {
        await using var host = await TestHost.CreateAsync();
        var client = await host.CreateClientAsync("client@example.test");

        Assert.IsNull(await host.Get<IProgressService>().SuggestCalorieTargetAsync(client.ClientId));
    }

    /// <summary>
    /// Spec 1.10: the plan schedules Monday, Tuesday, Thursday and Saturday; sessions on Monday, Tuesday and Thursday,
    /// plus one on an unscheduled Wednesday that doesn't count, give 3 of 4 = 75%.
    /// </summary>
    [TestMethod]
    public async Task GetAdherenceAsync_ThreeOfFourScheduledDays_Returns75Percent()
    {
        await using var host = await TestHost.CreateAsync();
        var trainer = await host.CreateTrainerAsync("trainer@example.test");
        var client = await host.CreateClientAsync("client@example.test");
        var run = await host.AddExerciseAsync("Run");
        var plan = await host.AddPlanAsync(trainer, client, PlanStatus.Active,
            Line(run, DayOfWeek.Monday), Line(run, DayOfWeek.Tuesday), Line(run, DayOfWeek.Thursday), Line(run, DayOfWeek.Saturday));
        plan.StartDate = new DateTime(2026, 10, 1);
        await host.Db.SaveChangesAsync();
        foreach (var day in new[] { 0, 1, 2, 3 })
        {
            await AddLogAsync(host, client, Monday12Oct.AddDays(day), (run, 100m));
        }

        var adherence = await host.Get<IProgressService>().GetAdherenceAsync(client.ClientId, Monday12Oct, Monday12Oct.AddDays(6));

        Assert.AreEqual(4, adherence.ScheduledDays);
        Assert.AreEqual(3, adherence.CompletedDays);
        Assert.AreEqual(75, adherence.Percent);
    }

    /// <summary>Days after today aren't missed yet, and days before the plan started weren't scheduled.</summary>
    [TestMethod]
    public async Task GetAdherenceAsync_RangeBeyondPlanAndToday_CountsOnlyPlannedPastDays()
    {
        await using var host = await TestHost.CreateAsync();
        var trainer = await host.CreateTrainerAsync("trainer@example.test");
        var client = await host.CreateClientAsync("client@example.test");
        var run = await host.AddExerciseAsync("Run");
        // AddPlanAsync starts client plans a week ago: Sunday 18 October.
        await host.AddPlanAsync(trainer, client, PlanStatus.Active, Line(run, DayOfWeek.Monday));
        await AddLogAsync(host, client, new DateTime(2026, 10, 19), (run, 100m));

        // 1 October to 31 December: only Monday 19 October lies between the plan's start and today.
        var adherence = await host.Get<IProgressService>().GetAdherenceAsync(client.ClientId, new DateTime(2026, 10, 1), new DateTime(2026, 12, 31));

        Assert.AreEqual(1, adherence.ScheduledDays);
        Assert.AreEqual(100, adherence.Percent);
    }

    /// <summary>A newer plan takes over from its start date; the completed plan counts before that (BR-09).</summary>
    [TestMethod]
    public async Task GetAdherenceAsync_PlanReplaced_UsesThePlanInEffectEachDay()
    {
        await using var host = await TestHost.CreateAsync();
        var trainer = await host.CreateTrainerAsync("trainer@example.test");
        var client = await host.CreateClientAsync("client@example.test");
        var run = await host.AddExerciseAsync("Run");
        var old = await host.AddPlanAsync(trainer, client, PlanStatus.Completed, Line(run, DayOfWeek.Monday));
        old.StartDate = new DateTime(2026, 10, 1);
        var current = await host.AddPlanAsync(trainer, client, PlanStatus.Active, Line(run, DayOfWeek.Friday));
        current.StartDate = new DateTime(2026, 10, 15);
        await host.Db.SaveChangesAsync();

        // 12–18 Oct: Monday 12 under the old plan, Friday 16 under the new one; Monday 19 is outside the range.
        var adherence = await host.Get<IProgressService>().GetAdherenceAsync(client.ClientId, Monday12Oct, Monday12Oct.AddDays(6));

        Assert.AreEqual(2, adherence.ScheduledDays);
        Assert.AreEqual(0, adherence.CompletedDays);
        Assert.AreEqual(0, adherence.Percent);
    }

    /// <summary>Nothing scheduled means no percentage rather than 0%.</summary>
    [TestMethod]
    public async Task GetAdherenceAsync_NoPlan_HasNoPercent()
    {
        await using var host = await TestHost.CreateAsync();
        var client = await host.CreateClientAsync("client@example.test");

        var adherence = await host.Get<IProgressService>().GetAdherenceAsync(client.ClientId, Monday12Oct, host.Clock.Today);

        Assert.AreEqual(0, adherence.ScheduledDays);
        Assert.IsNull(adherence.Percent);
    }

    /// <summary>BR-14, UC37: BMI is worked out from the saved weight and height; the audit row has no health values.</summary>
    [TestMethod]
    public async Task AddMeasurementAsync_WeightAndHeight_BmiCalculated()
    {
        await using var host = await TestHost.CreateAsync();
        var client = await host.CreateClientAsync("client@example.test");
        var progress = host.Get<IProgressService>();

        var id = await progress.AddMeasurementAsync(client.UserId, Form(host, 72.5m, 175m));

        // 72.5 ÷ 1.75² = 23.67 → 23.7, Normal.
        var stored = await host.Db.HealthMeasurements.AsNoTracking().SingleAsync();
        Assert.AreEqual(stored.MeasurementId, id);
        Assert.AreEqual(23.7m, stored.Bmi);
        var latest = await progress.GetLatestMeasurementAsync(client.ClientId);
        Assert.AreEqual(23.7m, latest!.Bmi);
        Assert.AreEqual(BmiCategory.Normal, latest.BmiCategory);
        Assert.AreEqual("Felt bloated", latest.Notes);

        var audit = await host.Db.AuditLogs.SingleAsync();
        Assert.AreEqual(AuditAction.Create, audit.Action);
        Assert.IsFalse(audit.Details!.Contains("72.5") || audit.Details.Contains("23.7") || audit.Details.Contains("bloated"));
    }

    /// <summary>BR-13: a measurement can't be in the future.</summary>
    [TestMethod]
    public async Task AddMeasurementAsync_FutureTime_ThrowsBr13()
    {
        await using var host = await TestHost.CreateAsync();
        var client = await host.CreateClientAsync("client@example.test");
        var form = Form(host, 70m, 170m);
        form.MeasuredAt = host.Clock.Now.AddMinutes(5);

        await RuleAssert.BrokenAsync("BR-13", () => host.Get<IProgressService>().AddMeasurementAsync(client.UserId, form));
        Assert.IsFalse(await host.Db.HealthMeasurements.AnyAsync());
    }

    /// <summary>The form pre-fills the height from the last reading.</summary>
    [TestMethod]
    public async Task GetMeasurementFormAsync_PreviousReading_PrefillsHeight()
    {
        await using var host = await TestHost.CreateAsync();
        var client = await host.CreateClientAsync("client@example.test");
        await host.AddMeasurementAsync(client, 70m);

        var form = await host.Get<IProgressService>().GetMeasurementFormAsync(client.ClientId);

        Assert.AreEqual(170m, form.HeightCm);
        Assert.IsNull(form.WeightKg);
        Assert.AreEqual(host.Clock.Now, form.MeasuredAt);
    }

    /// <summary>One point a day (the last reading), with the target weight as a dashed line on the same days.</summary>
    [TestMethod]
    public async Task GetWeightTrendAsync_ReadingsAndTarget_OnePointADayWithDashedTarget()
    {
        await using var host = await TestHost.CreateAsync();
        var client = await host.CreateClientAsync("client@example.test");
        client.TargetWeightKg = 68m;
        await host.Db.SaveChangesAsync();
        await host.AddMeasurementAsync(client, 75m, new DateTime(2026, 10, 1, 7, 0, 0));
        await host.AddMeasurementAsync(client, 74m, new DateTime(2026, 10, 20, 7, 0, 0));
        await host.AddMeasurementAsync(client, 73.5m, new DateTime(2026, 10, 20, 19, 0, 0));
        await host.AddMeasurementAsync(client, 73m, new DateTime(2026, 10, 24, 7, 0, 0));

        var series = await host.Get<IProgressService>().GetWeightTrendAsync(client.ClientId, new DateTime(2026, 10, 15), host.Clock.Today);

        Assert.HasCount(2, series.Labels);
        CollectionAssert.AreEqual(new decimal?[] { 73.5m, 73m }, series.Datasets[0].Data.ToArray());
        Assert.IsTrue(series.Datasets[1].Dashed);
        CollectionAssert.AreEqual(new decimal?[] { 68m, 68m }, series.Datasets[1].Data.ToArray());
        Assert.IsFalse(series.IsEmpty);
    }

    /// <summary>
    /// Weekly bars over 12–25 October: kcal burned and eaten per Monday-start week, against the daily target added up
    /// over the days a diet plan applied (the plan starts on Monday 19 October).
    /// </summary>
    [TestMethod]
    public async Task GetCaloriesTrendAsync_TwoWeeks_SumsBurnedEatenAndTarget()
    {
        await using var host = await TestHost.CreateAsync();
        var trainer = await host.CreateTrainerAsync("trainer@example.test");
        var client = await host.CreateClientAsync("client@example.test");
        var run = await host.AddExerciseAsync("Run");
        await AddLogAsync(host, client, new DateTime(2026, 10, 13), (run, 250m), (run, 100.4m));
        await AddLogAsync(host, client, new DateTime(2026, 10, 21), (run, 300m));
        await host.AddDietPlanAsync(trainer, client, PlanStatus.Active, 2000, new DateTime(2026, 10, 19), TestHost.Meal(MealType.Lunch));
        var rice = await host.AddFoodAsync("Rice", 130m);
        foreach (var (date, servings) in new[] { (new DateTime(2026, 10, 20), 10m), (new DateTime(2026, 10, 22), 5m) })
        {
            var meal = new MealLog { ClientId = client.ClientId, MealDate = date, MealType = MealType.Lunch, CreatedAt = host.Clock.Now };
            meal.AddItem(rice, servings);
            host.Db.MealLogs.Add(meal);
        }

        await host.Db.SaveChangesAsync();

        var series = await host.Get<IProgressService>().GetCaloriesTrendAsync(client.ClientId, Monday12Oct, host.Clock.Today);

        Assert.HasCount(2, series.Labels);
        CollectionAssert.AreEqual(new decimal?[] { 350m, 300m }, series.Datasets[0].Data.ToArray());
        CollectionAssert.AreEqual(new decimal?[] { null, 1950m }, series.Datasets[1].Data.ToArray());
        CollectionAssert.AreEqual(new decimal?[] { null, 14000m }, series.Datasets[2].Data.ToArray());
        Assert.AreEqual("bar", series.Datasets[0].Type);
        Assert.IsTrue(series.Datasets[2].Dashed);
    }

    /// <summary>Vitals keep only the days with a heart-rate or blood-pressure reading.</summary>
    [TestMethod]
    public async Task GetVitalsTrendAsync_SomeReadingsWithoutVitals_SkipsThem()
    {
        await using var host = await TestHost.CreateAsync();
        var client = await host.CreateClientAsync("client@example.test");
        await host.AddMeasurementAsync(client, 70m, new DateTime(2026, 10, 20, 8, 0, 0));
        var withVitals = await host.AddMeasurementAsync(client, 70m, new DateTime(2026, 10, 22, 8, 0, 0));
        withVitals.RestingHeartRate = 62;
        withVitals.SystolicBp = 118;
        withVitals.DiastolicBp = 76;
        await host.Db.SaveChangesAsync();

        var series = await host.Get<IProgressService>().GetVitalsTrendAsync(client.ClientId, Monday12Oct, host.Clock.Today);

        Assert.HasCount(1, series.Labels);
        Assert.AreEqual(62m, series.Datasets[0].Data[0]);
        Assert.AreEqual(118m, series.Datasets[1].Data[0]);
        Assert.AreEqual(76m, series.Datasets[2].Data[0]);
    }

    /// <summary>An empty range gives series with nothing to draw.</summary>
    [TestMethod]
    public async Task GetBmiTrendAsync_NoReadings_IsEmpty()
    {
        await using var host = await TestHost.CreateAsync();
        var client = await host.CreateClientAsync("client@example.test");

        Assert.IsTrue((await host.Get<IProgressService>().GetBmiTrendAsync(client.ClientId, Monday12Oct, host.Clock.Today)).IsEmpty);
    }

    /// <summary>BR-08: a trainer can't open the progress of a client who isn't assigned to them, or no longer is.</summary>
    [TestMethod]
    public async Task GetTrainerClientProgressAsync_UnassignedClient_ReturnsNull()
    {
        await using var host = await TestHost.CreateAsync();
        var trainer = await host.CreateTrainerAsync("trainer@example.test");
        var stranger = await host.CreateTrainerAsync("stranger@example.test");
        var former = await host.CreateTrainerAsync("former@example.test");
        var client = await host.CreateClientAsync("client@example.test");
        await host.AssignAsync(former, client, endDate: host.Clock.Today.AddDays(-10), startDate: host.Clock.Today.AddMonths(-6));
        await host.AssignAsync(trainer, client, startDate: host.Clock.Today.AddDays(-10));
        var range = ProgressRangeViewModel.Resolve(null, null, null, host.Clock.Today);
        var progress = host.Get<IProgressService>();

        Assert.IsNull(await progress.GetTrainerClientProgressAsync(stranger.UserId, client.ClientId, range));
        Assert.IsNull(await progress.GetTrainerClientProgressAsync(former.UserId, client.ClientId, range));
        Assert.IsNotNull(await progress.GetTrainerClientProgressAsync(trainer.UserId, client.ClientId, range));
    }

    /// <summary>The client decides: with sharing off, the trainer sees no medical fields and no measurement notes.</summary>
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task GetTrainerClientProgressAsync_Sharing_ControlsMedicalFields(bool shares)
    {
        await using var host = await TestHost.CreateAsync();
        var trainer = await host.CreateTrainerAsync("trainer@example.test");
        var client = await host.CreateClientAsync("client@example.test");
        client.ShareMedicalInfoWithTrainer = shares;
        client.MedicalConditions = "Asthma";
        client.Allergies = "Peanuts";
        await host.Db.SaveChangesAsync();
        await host.AssignAsync(trainer, client);
        var measurement = await host.AddMeasurementAsync(client, 70m);
        measurement.Notes = "Private note";
        await host.Db.SaveChangesAsync();

        var model = await host.Get<IProgressService>().GetTrainerClientProgressAsync(trainer.UserId, client.ClientId,
            ProgressRangeViewModel.Resolve(null, null, null, host.Clock.Today));

        Assert.AreEqual(shares, model!.SharesMedicalInfo);
        Assert.AreEqual(shares ? "Asthma" : null, model.MedicalConditions);
        Assert.AreEqual(shares ? "Peanuts" : null, model.Allergies);
        Assert.AreEqual(shares ? "Private note" : null, model.Latest!.Notes);
        Assert.AreEqual(70m, model.Latest.WeightKg);
        Assert.AreEqual(36, model.Age);
    }
}
