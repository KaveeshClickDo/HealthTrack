using System.Text;
using HealthTrack.Helpers;
using HealthTrack.Models.Entities;
using HealthTrack.Models.Enums;
using HealthTrack.Models.ViewModels.Reports;
using HealthTrack.Services;
using HealthTrack.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Tests.Services;

/// <summary>
/// The reports of spec 1.7 on a small dataset: Chris coaches Amaya and Ravi, Olivia coaches Sam. The test clock reads
/// Sunday 25 October 2026, so the default range is 26 September to 25 October.
/// </summary>
[TestClass]
public sealed class ReportServiceTests
{
    private static readonly DateTime Today = new(2026, 10, 25);
    private static readonly DateTime Monday12 = new(2026, 10, 12);
    private static readonly DateTime Monday19 = new(2026, 10, 19);

    private static ReportRangeViewModel Last30Days => ReportRangeViewModel.Resolve(null, null, Today);

    private static async Task<(TestHost Host, TrainerProfile Chris, TrainerProfile Olivia, ClientProfile Amaya, ClientProfile Ravi, ClientProfile Sam)> ArrangeAsync()
    {
        var host = await TestHost.CreateAsync();
        var chris = await host.CreateTrainerAsync("chris@example.test", firstName: "Chris");
        var olivia = await host.CreateTrainerAsync("olivia@example.test", firstName: "Olivia");
        var amaya = await host.CreateClientAsync("amaya@example.test", "Amaya");
        var ravi = await host.CreateClientAsync("ravi@example.test", "Ravi");
        var sam = await host.CreateClientAsync("sam@example.test", "Sam");
        await host.AssignAsync(chris, amaya);
        await host.AssignAsync(chris, ravi);
        await host.AssignAsync(olivia, sam);
        return (host, chris, olivia, amaya, ravi, sam);
    }

    // 5 MET × 70 kg × 30 minutes / 60 = 175 kcal.
    private static async Task LogWorkoutAsync(TestHost host, ClientProfile client, Exercise exercise, DateTime day, WorkoutPlan? plan = null)
    {
        var log = new WorkoutLog { ClientId = client.ClientId, WorkoutPlanId = plan?.WorkoutPlanId, WorkoutDate = day, CreatedAt = day.AddHours(18) };
        log.AddEntry(exercise, 70m, 30);
        host.Db.WorkoutLogs.Add(log);
        await host.Db.SaveChangesAsync();
    }

    private static async Task LogMealAsync(TestHost host, ClientProfile client, FoodItem food, DateTime day, decimal servings)
    {
        var log = new MealLog { ClientId = client.ClientId, MealDate = day, MealType = MealType.Lunch, CreatedAt = day.AddHours(13) };
        log.AddItem(food, servings);
        host.Db.MealLogs.Add(log);
        await host.Db.SaveChangesAsync();
    }

    private static async Task<WorkoutPlan> CopyAsync(TestHost host, WorkoutPlan template, ClientProfile client, TrainerProfile trainer, DateTime start)
    {
        var plan = template.CreateCopyForClient(client.ClientId, trainer.TrainerId, start);
        plan.CreatedAt = start;
        plan.Activate();
        host.Db.WorkoutPlans.Add(plan);
        await host.Db.SaveChangesAsync();
        return plan;
    }

    private static async Task LoginAsync(TestHost host, string userId, DateTime at)
    {
        host.Db.AuditLogs.Add(new AuditLog { UserId = userId, Action = AuditAction.Login, Timestamp = at });
        await host.Db.SaveChangesAsync();
    }

    /// <summary>BR-08: a trainer's report lists only their assigned clients; the administrator's lists everyone.</summary>
    [TestMethod]
    public async Task GetClientPerformanceAsync_TrainerScope_LeavesOutOtherTrainersClients()
    {
        var (host, chris, _, _, _, _) = await ArrangeAsync();
        await using var _ = host;
        var reports = host.Get<IReportService>();

        var mine = await reports.GetClientPerformanceAsync(Last30Days, chris.UserId);
        var everyone = await reports.GetClientPerformanceAsync(Last30Days);

        CollectionAssert.AreEqual(new[] { "Amaya User", "Ravi User" }, mine.Rows.Select(r => r.ClientName).ToArray());
        Assert.IsFalse(mine.AllClients);
        CollectionAssert.AreEqual(new[] { "Amaya User", "Ravi User", "Sam User" }, everyone.Rows.Select(r => r.ClientName).ToArray());
        Assert.AreEqual("Olivia User", everyone.Rows[2].TrainerName);
    }

    /// <summary>
    /// Spec 1.7: start weight is the last reading before the range; adherence follows the plan (one of two Mondays logged);
    /// average eating is per logged day and the target only where a diet plan applied; a trainer counts only the
    /// appointments they held.
    /// </summary>
    [TestMethod]
    public async Task GetClientPerformanceAsync_SmallDataset_ComputesEachColumn()
    {
        var (host, chris, olivia, amaya, _, _) = await ArrangeAsync();
        await using var _ = host;
        await host.AddMeasurementAsync(amaya, 80m, Today.AddDays(-40));
        await host.AddMeasurementAsync(amaya, 79m, Today.AddDays(-20));
        await host.AddMeasurementAsync(amaya, 78m, Today.AddDays(-2));

        var squat = await host.AddExerciseAsync("Squat");
        var plan = await host.AddPlanAsync(chris, amaya, PlanStatus.Active,
            new WorkoutPlanExercise { ExerciseId = squat.ExerciseId, DayOfWeek = DayOfWeek.Monday, Sets = 3, Reps = 10 });
        plan.StartDate = Today.AddDays(-14);
        await host.Db.SaveChangesAsync();
        await LogWorkoutAsync(host, amaya, squat, Monday12, plan);
        await LogWorkoutAsync(host, amaya, squat, new DateTime(2026, 10, 20));

        var rice = await host.AddFoodAsync("Rice", 200m);
        await host.AddDietPlanAsync(chris, amaya, PlanStatus.Active, dailyCalories: 2000, startDate: Today.AddDays(-10), meals: [TestHost.Meal(MealType.Lunch)]);
        await LogMealAsync(host, amaya, rice, new DateTime(2026, 10, 1), 7.5m);
        await LogMealAsync(host, amaya, rice, new DateTime(2026, 10, 22), 9m);
        await LogMealAsync(host, amaya, rice, new DateTime(2026, 10, 23), 11m);

        await host.AddAppointmentAsync(amaya, chris, new DateTime(2026, 10, 20, 10, 0, 0), status: AppointmentStatus.Completed);
        await host.AddAppointmentAsync(amaya, olivia, new DateTime(2026, 10, 21, 10, 0, 0), status: AppointmentStatus.Completed);
        await host.AddAppointmentAsync(amaya, chris, new DateTime(2026, 9, 1, 10, 0, 0), status: AppointmentStatus.Completed);
        var reports = host.Get<IReportService>();

        var row = (await reports.GetClientPerformanceAsync(Last30Days, chris.UserId)).Rows.Single(r => r.ClientId == amaya.ClientId);
        var adminRow = (await reports.GetClientPerformanceAsync(Last30Days)).Rows.Single(r => r.ClientId == amaya.ClientId);

        Assert.AreEqual(80m, row.StartWeightKg);
        Assert.AreEqual(78m, row.LatestWeightKg);
        Assert.AreEqual(-2m, row.WeightChangeKg);
        Assert.AreEqual(HealthCalculator.CalculateBmi(80m, 170m), row.StartBmi);
        Assert.AreEqual(2, row.WorkoutsCompleted);
        Assert.AreEqual(50, row.AdherencePercent);
        Assert.AreEqual(1833m, row.AverageDailyCalories);
        Assert.AreEqual(2000m, row.AverageDailyTarget);
        Assert.AreEqual(350m, row.CaloriesBurned);
        Assert.AreEqual(1, row.AppointmentsAttended);
        Assert.AreEqual(2, adminRow.AppointmentsAttended);
    }

    /// <summary>
    /// Templates rank by the clients following a copy in the range (BR-12), with pooled adherence; the top exercises and
    /// category shares count only the clients in scope.
    /// </summary>
    [TestMethod]
    public async Task GetPopularRoutinesAsync_CopiesAndLogs_RanksTemplatesAndExercises()
    {
        var (host, chris, olivia, amaya, ravi, sam) = await ArrangeAsync();
        await using var _ = host;
        var squat = await host.AddExerciseAsync("Squat");
        var run = await host.AddExerciseAsync("Run", 8m);
        run.Category = ExerciseCategory.Cardio;
        var template = await host.AddPlanAsync(chris, null, PlanStatus.Draft,
            new WorkoutPlanExercise { ExerciseId = squat.ExerciseId, DayOfWeek = DayOfWeek.Monday, Sets = 3, Reps = 10 });
        template.Title = "Full body";
        var unused = await host.AddPlanAsync(chris, null);
        unused.Title = "Unused";
        await host.Db.SaveChangesAsync();

        var amayaPlan = await CopyAsync(host, template, amaya, chris, Today.AddDays(-14));
        var raviPlan = await CopyAsync(host, template, ravi, chris, Today.AddDays(-14));
        var samPlan = await CopyAsync(host, template, sam, olivia, Today.AddDays(-14));
        await LogWorkoutAsync(host, amaya, squat, Monday12, amayaPlan);
        await LogWorkoutAsync(host, ravi, squat, Monday12, raviPlan);
        await LogWorkoutAsync(host, ravi, squat, Monday19, raviPlan);
        await LogWorkoutAsync(host, amaya, run, new DateTime(2026, 10, 20));
        await LogWorkoutAsync(host, sam, squat, Monday19, samPlan);
        var reports = host.Get<IReportService>();

        var mine = await reports.GetPopularRoutinesAsync(Last30Days, chris.UserId);
        var everyone = await reports.GetPopularRoutinesAsync(Last30Days);

        CollectionAssert.AreEqual(new[] { "Full body", "Unused" }, mine.Routines.Select(r => r.Title).ToArray());
        Assert.AreEqual(2, mine.Routines[0].ClientsAssigned);
        Assert.AreEqual(2, mine.Routines[0].ActivePlans);
        Assert.AreEqual(75, mine.Routines[0].AdherencePercent);
        Assert.AreEqual(0, mine.Routines[1].ClientsAssigned);
        Assert.IsNull(mine.Routines[1].AdherencePercent);
        Assert.AreEqual(3, everyone.Routines.Single(r => r.Title == "Full body").ClientsAssigned);

        CollectionAssert.AreEqual(new[] { "Squat", "Run" }, mine.TopExercises.Select(e => e.ExerciseName).ToArray());
        Assert.AreEqual(3, mine.TopExercises[0].TimesLogged);
        Assert.AreEqual(90, mine.TopExercises[0].TotalMinutes);
        CollectionAssert.AreEqual(new[] { 75.0m, 25.0m }, mine.Categories.Select(c => c.Percent).ToArray());
        Assert.AreEqual("Strength", mine.Categories[0].Label);
        Assert.AreEqual(4, everyone.TopExercises[0].TimesLogged);
    }

    /// <summary>
    /// Engagement week by week: activity of the clients in scope, their logins (not the trainer's own), the trainer's own
    /// appointments and their clients' registrations; only administrators get the trainer workload.
    /// </summary>
    [TestMethod]
    public async Task GetEngagementAsync_TrainerScope_CountsOnlyTheirClients()
    {
        var (host, chris, olivia, amaya, ravi, sam) = await ArrangeAsync();
        await using var _ = host;
        var squat = await host.AddExerciseAsync("Squat");
        var rice = await host.AddFoodAsync("Rice", 200m);
        await LogWorkoutAsync(host, amaya, squat, new DateTime(2026, 10, 24));
        await LogMealAsync(host, amaya, rice, new DateTime(2026, 10, 23), 2m);
        await LogWorkoutAsync(host, ravi, squat, new DateTime(2026, 10, 10));
        await LogWorkoutAsync(host, sam, squat, new DateTime(2026, 10, 24));
        await LoginAsync(host, amaya.UserId, new DateTime(2026, 10, 24, 10, 0, 0));
        await LoginAsync(host, amaya.UserId, new DateTime(2026, 10, 23, 9, 0, 0));
        await LoginAsync(host, chris.UserId, new DateTime(2026, 10, 24, 8, 0, 0));
        await LoginAsync(host, sam.UserId, new DateTime(2026, 10, 24, 8, 0, 0));
        await host.AddAppointmentAsync(amaya, chris, new DateTime(2026, 10, 20, 10, 0, 0), status: AppointmentStatus.Completed);
        await host.AddAppointmentAsync(ravi, chris, new DateTime(2026, 10, 22, 10, 0, 0), status: AppointmentStatus.Approved);
        await host.AddAppointmentAsync(sam, olivia, new DateTime(2026, 10, 21, 10, 0, 0), status: AppointmentStatus.Completed);
        var reports = host.Get<IReportService>();

        var mine = await reports.GetEngagementAsync(Last30Days, chris.UserId);
        var everyone = await reports.GetEngagementAsync(Last30Days);

        Assert.AreEqual(2, mine.ClientsInScope);
        Assert.AreEqual(1, mine.ActiveClients);
        CollectionAssert.AreEqual(
            new[] { new DateTime(2026, 9, 21), new DateTime(2026, 9, 28), new DateTime(2026, 10, 5), Monday12, Monday19 },
            mine.Weeks.Select(w => w.WeekStart).ToArray());
        var lastWeek = mine.Weeks[^1];
        Assert.AreEqual(1, lastWeek.ActiveClients);
        Assert.AreEqual(1, lastWeek.WorkoutLogs);
        Assert.AreEqual(1, lastWeek.MealLogs);
        Assert.AreEqual(1.0m, lastWeek.LogsPerClient);
        Assert.AreEqual(2, lastWeek.Logins);
        Assert.AreEqual(1, mine.Weeks.Single(w => w.WeekStart == new DateTime(2026, 10, 5)).WorkoutLogs);
        Assert.HasCount(30, mine.Chart.Labels);
        Assert.AreEqual(2m, mine.Chart.Datasets[0].Data.Sum());

        Assert.AreEqual(1, mine.AppointmentsByStatus.Single(s => s.Label == "Completed").Count);
        Assert.AreEqual(1, mine.AppointmentsByStatus.Single(s => s.Label == AppointmentStatus.Approved.GetDisplayName()).Count);
        Assert.AreEqual(2, mine.RegistrationsByMonth.Single(m => m.Label == "Oct 2026").Count);
        Assert.IsEmpty(mine.TrainerWorkload);

        Assert.AreEqual(4m, everyone.Chart.Datasets[0].Data.Sum());
        Assert.AreEqual(2, everyone.AppointmentsByStatus.Single(s => s.Label == "Completed").Count);
        Assert.HasCount(2, everyone.TrainerWorkload);
    }

    /// <summary>Each approved professional's clients against capacity, and their appointments, plans and feedback in the range.</summary>
    [TestMethod]
    public async Task GetTrainerWorkloadAsync_TwoTrainers_CountsEachOnesWork()
    {
        var (host, chris, olivia, amaya, ravi, sam) = await ArrangeAsync();
        await using var _ = host;
        await host.CreateTrainerAsync("pending@example.test", approved: false);
        await host.AddAppointmentAsync(amaya, chris, new DateTime(2026, 10, 20, 10, 0, 0), status: AppointmentStatus.Completed);
        await host.AddAppointmentAsync(ravi, chris, new DateTime(2026, 10, 22, 10, 0, 0), status: AppointmentStatus.Approved);
        await host.AddAppointmentAsync(ravi, chris, new DateTime(2026, 8, 22, 10, 0, 0), status: AppointmentStatus.Completed);
        await host.AddPlanAsync(chris, amaya);
        await host.AddPlanAsync(chris, null);
        host.Db.Feedback.Add(new Feedback { TrainerId = chris.TrainerId, ClientId = amaya.ClientId, Category = FeedbackCategory.General, Message = "Well done", CreatedAt = new DateTime(2026, 10, 22) });
        await host.Db.SaveChangesAsync();

        var report = await host.Get<IReportService>().GetTrainerWorkloadAsync(Last30Days);

        CollectionAssert.AreEqual(new[] { "Chris User", "Olivia User" }, report.Rows.Select(r => r.TrainerName).ToArray());
        var row = report.Rows[0];
        Assert.AreEqual(2, row.ActiveClients);
        Assert.AreEqual(20, row.MaxClients);
        Assert.AreEqual(10, row.LoadPercent);
        Assert.AreEqual(2, row.Appointments);
        Assert.AreEqual(1, row.AppointmentsCompleted);
        Assert.AreEqual(1, row.PlansCreated);
        Assert.AreEqual(1, row.FeedbackGiven);
        Assert.AreEqual(1, report.Rows[1].ActiveClients);
    }

    /// <summary>UC40: the export is a UTF-8 CSV named after the report and today, and it is audited.</summary>
    [TestMethod]
    public async Task ExportCsvAsync_ClientPerformance_ReturnsCsvAndAudits()
    {
        var (host, chris, _, amaya, _, _) = await ArrangeAsync();
        await using var _ = host;
        await host.AddMeasurementAsync(amaya, 80m, Today.AddDays(-3));

        var file = await host.Get<IReportService>().ExportCsvAsync(ReportService.ClientPerformance, Last30Days, chris.UserId, chris.UserId);

        Assert.IsNotNull(file);
        Assert.AreEqual("client-performance-20261025.csv", file.FileName);
        Assert.AreEqual("text/csv", file.ContentType);
        var lines = Encoding.UTF8.GetString(file.Content).TrimStart('﻿').Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.StartsWith("Client,Trainer,Start weight (kg)", lines[0]);
        Assert.HasCount(3, lines);
        Assert.StartsWith("Amaya User,Chris User,80.0,80.0,0.0,", lines[1]);

        var audit = await host.Db.AuditLogs.SingleAsync();
        Assert.AreEqual(AuditAction.Export, audit.Action);
        Assert.AreEqual(ReportService.ClientPerformance, audit.EntityId);
        Assert.AreEqual(chris.UserId, audit.UserId);
    }

    /// <summary>The trainer workload is for administrators only, and an unknown report exports nothing.</summary>
    [TestMethod]
    public async Task ExportCsvAsync_WorkloadForTrainerOrUnknownReport_ReturnsNull()
    {
        var (host, chris, _, _, _, _) = await ArrangeAsync();
        await using var _ = host;
        var reports = host.Get<IReportService>();

        Assert.IsNull(await reports.ExportCsvAsync(ReportService.TrainerWorkload, Last30Days, chris.UserId, chris.UserId));
        Assert.IsNull(await reports.ExportCsvAsync("passwords", Last30Days, null, chris.UserId));
        Assert.IsNotNull(await reports.ExportCsvAsync(ReportService.TrainerWorkload, Last30Days, null, chris.UserId));
        Assert.AreEqual(1, await host.Db.AuditLogs.CountAsync());
    }

    /// <summary>The range is the last 30 days by default; dates in the wrong order are swapped.</summary>
    [TestMethod]
    public void ReportRangeViewModel_Resolve_DefaultsAndSwaps()
    {
        var defaults = ReportRangeViewModel.Resolve(null, null, Today);
        var swapped = ReportRangeViewModel.Resolve(Today, Today.AddDays(-7), Today);

        Assert.AreEqual(new DateTime(2026, 9, 26), defaults.From);
        Assert.AreEqual(Today, defaults.To);
        Assert.AreEqual(Today.AddDays(-7), swapped.From);
        Assert.AreEqual(Today, swapped.To);
    }
}
