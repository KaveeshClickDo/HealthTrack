using HealthTrack.Data;
using HealthTrack.Models.Entities;
using HealthTrack.Models.Enums;
using HealthTrack.Security;
using HealthTrack.Services;
using HealthTrack.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Tests.Services;

/// <summary>
/// The three dashboards (spec 1.4). The test clock reads Sunday 25 October 2026, 09:00, so this week began on Monday 19 October.
/// </summary>
[TestClass]
public sealed class DashboardServiceTests
{
    private static readonly DateTime Today = new(2026, 10, 25);

    private static async Task LogWorkoutAsync(TestHost host, ClientProfile client, Exercise exercise, DateTime day)
    {
        var log = new WorkoutLog { ClientId = client.ClientId, WorkoutDate = day, CreatedAt = day.AddHours(8) };
        log.AddEntry(exercise, 70m, 30);
        host.Db.WorkoutLogs.Add(log);
        await host.Db.SaveChangesAsync();
    }

    private static async Task NotifyAsync(TestHost host, string userId, string title, bool read)
    {
        var notification = new Notification { UserId = userId, Type = NotificationType.System, Title = title, Message = "Message", CreatedAt = host.Clock.Now };
        if (read)
        {
            notification.MarkAsRead(host.Clock.Now);
        }

        host.Db.Notifications.Add(notification);
        await host.Db.SaveChangesAsync();
    }

    /// <summary>
    /// UC07: latest weight and BMI, the change since the first weigh-in, this week's burned kcal (Monday on), the next 3
    /// appointments, the newest feedback and the unread notifications.
    /// </summary>
    [TestMethod]
    public async Task GetClientDashboardAsync_WithHistory_FillsTheStats()
    {
        await using var host = await TestHost.CreateAsync();
        var trainer = await host.CreateTrainerAsync("trainer@example.test", firstName: "Chris");
        var client = await host.CreateClientAsync("client@example.test", "Amaya");
        await host.AssignAsync(trainer, client);
        await host.AddMeasurementAsync(client, 80m, Today.AddDays(-30));
        await host.AddMeasurementAsync(client, 78.5m, Today.AddDays(-1));
        var squat = await host.AddExerciseAsync("Squat");
        await LogWorkoutAsync(host, client, squat, new DateTime(2026, 10, 20));
        await LogWorkoutAsync(host, client, squat, new DateTime(2026, 10, 12));
        for (var day = 1; day <= 4; day++)
        {
            await host.AddAppointmentAsync(client, trainer, Today.AddDays(day).AddHours(10), status: AppointmentStatus.Approved);
        }

        host.Db.Feedback.Add(new Feedback { TrainerId = trainer.TrainerId, ClientId = client.ClientId, Category = FeedbackCategory.General, Message = "Older", CreatedAt = Today.AddDays(-5) });
        host.Db.Feedback.Add(new Feedback { TrainerId = trainer.TrainerId, ClientId = client.ClientId, Category = FeedbackCategory.Progress, Message = "Newest", Rating = 5, CreatedAt = Today.AddDays(-1) });
        await host.Db.SaveChangesAsync();
        await NotifyAsync(host, client.UserId, "Read", read: true);
        await NotifyAsync(host, client.UserId, "Unread one", read: false);
        await NotifyAsync(host, client.UserId, "Unread two", read: false);

        var model = (await host.Get<IDashboardService>().GetClientDashboardAsync(client.UserId))!;

        Assert.AreEqual("Amaya", model.FirstName);
        Assert.AreEqual("Chris User", model.TrainerName);
        Assert.AreEqual(78.5m, model.LatestWeightKg);
        Assert.AreEqual(-1.5m, model.WeightChangeKg);
        Assert.AreEqual(BmiCategory.Overweight, model.BmiCategory);
        Assert.AreEqual(175m, model.CaloriesBurnedThisWeek);
        Assert.AreEqual(1, model.WorkoutsThisWeek);
        CollectionAssert.AreEqual(new[] { Today.AddDays(1).AddHours(10), Today.AddDays(2).AddHours(10), Today.AddDays(3).AddHours(10) },
            model.UpcomingAppointments.Select(a => a.StartDateTime).ToArray());
        Assert.AreEqual("Newest", model.LatestFeedback!.Message);
        Assert.AreEqual(2, model.UnreadCount);
        Assert.HasCount(2, model.UnreadNotifications);
    }

    /// <summary>Today's workout lists the plan's exercises until a session is logged today; without a plan there is none.</summary>
    [TestMethod]
    public async Task GetClientDashboardAsync_TodaysWorkout_StartUntilLogged()
    {
        await using var host = await TestHost.CreateAsync();
        var trainer = await host.CreateTrainerAsync("trainer@example.test");
        var client = await host.CreateClientAsync("client@example.test");
        var squat = await host.AddExerciseAsync("Squat");
        var dashboards = host.Get<IDashboardService>();

        Assert.IsFalse((await dashboards.GetClientDashboardAsync(client.UserId))!.TodaysWorkout.HasPlan);

        await host.AddPlanAsync(trainer, client, PlanStatus.Active,
            new WorkoutPlanExercise { ExerciseId = squat.ExerciseId, DayOfWeek = DayOfWeek.Sunday, Sets = 3, Reps = 10 });
        var before = (await dashboards.GetClientDashboardAsync(client.UserId))!.TodaysWorkout;
        await LogWorkoutAsync(host, client, squat, Today);
        var after = (await dashboards.GetClientDashboardAsync(client.UserId))!.TodaysWorkout;

        Assert.IsTrue(before.HasPlan);
        Assert.IsFalse(before.IsRestDay);
        Assert.StartsWith("Squat – ", before.Exercises.Single());
        Assert.IsFalse(before.LoggedToday);
        Assert.IsTrue(after.LoggedToday);
        Assert.IsNull(await dashboards.GetClientDashboardAsync(trainer.UserId));
    }

    /// <summary>
    /// UC17: clients who have done nothing for 7 days come first and are flagged; pending requests and today's confirmed
    /// appointments are listed.
    /// </summary>
    [TestMethod]
    public async Task GetTrainerDashboardAsync_ClientsAndAppointments_FlagsInactiveAndListsToday()
    {
        await using var host = await TestHost.CreateAsync();
        var trainer = await host.CreateTrainerAsync("trainer@example.test", firstName: "Chris");
        var active = await host.CreateClientAsync("active@example.test", "Amaya");
        var quiet = await host.CreateClientAsync("quiet@example.test", "Bea");
        await host.AssignAsync(trainer, active);
        await host.AssignAsync(trainer, quiet);
        var squat = await host.AddExerciseAsync("Squat");
        await LogWorkoutAsync(host, active, squat, Today.AddDays(-1));
        await host.AddMeasurementAsync(active, 70m, Today.AddDays(-20));
        await host.AddMeasurementAsync(active, 69.2m, Today.AddDays(-2));
        await host.AddAppointmentAsync(active, trainer, Today.AddDays(2).AddHours(10), status: AppointmentStatus.Pending);
        await host.AddAppointmentAsync(quiet, trainer, Today.AddHours(15), status: AppointmentStatus.Approved);
        await host.AddAppointmentAsync(active, trainer, Today.AddDays(1).AddHours(9), status: AppointmentStatus.Approved);

        var model = (await host.Get<IDashboardService>().GetTrainerDashboardAsync(trainer.UserId))!;

        CollectionAssert.AreEqual(new[] { "Bea User", "Amaya User" }, model.Clients.Select(c => c.FullName).ToArray());
        Assert.IsTrue(model.Clients[0].IsInactive);
        Assert.IsNull(model.Clients[0].LastActiveDay);
        Assert.IsFalse(model.Clients[1].IsInactive);
        Assert.AreEqual(Today.AddDays(-1), model.Clients[1].LastActiveDay);
        Assert.AreEqual(-0.8m, model.Clients[1].WeightChangeKg);
        Assert.AreEqual(1, model.InactiveClients);
        Assert.AreEqual(Today.AddDays(2).AddHours(10), model.PendingRequests.Single().StartDateTime);
        Assert.AreEqual("Bea User", model.TodaysSchedule.Single().ClientName);
    }

    /// <summary>
    /// UC28: users by role and status, new registrations this month, active users, approvals, unassigned clients, logins
    /// per day for 14 days and appointments by status.
    /// </summary>
    [TestMethod]
    public async Task GetAdminDashboardAsync_MixedUsers_CountsEverything()
    {
        await using var host = await TestHost.CreateAsync();
        var admin = await host.CreateUserAsync("admin@example.test", AppRoles.Admin, firstName: "Ada");
        var trainer = await host.CreateTrainerAsync("trainer@example.test");
        await host.CreateTrainerAsync("pending@example.test", approved: false);
        var assigned = await host.CreateClientAsync("assigned@example.test", "Amaya");
        await host.CreateClientAsync("unassigned@example.test", "Una");
        var suspended = await host.CreateClientAsync("suspended@example.test", "Sue", AccountStatus.Suspended);
        await host.AssignAsync(trainer, assigned);

        // Registered last month; signed in yesterday.
        assigned.User.CreatedAt = new DateTime(2026, 9, 30);
        assigned.User.LastLoginAt = Today.AddDays(-1);
        suspended.User.LastLoginAt = Today.AddDays(-20);
        foreach (var at in new[] { Today.AddHours(7), Today.AddHours(8), Today.AddHours(8.5), Today.AddDays(-1).AddHours(9), Today.AddDays(-20) })
        {
            host.Db.AuditLogs.Add(new AuditLog { UserId = admin.Id, Action = AuditAction.Login, Timestamp = at });
        }

        await host.Db.SaveChangesAsync();
        await host.AddAppointmentAsync(assigned, trainer, Today.AddDays(-3), status: AppointmentStatus.Completed);
        await host.AddAppointmentAsync(assigned, trainer, Today.AddDays(3), status: AppointmentStatus.Pending);

        var model = await host.Get<IDashboardService>().GetAdminDashboardAsync(admin.Id);

        Assert.AreEqual("Ada", model.FirstName);
        var clients = model.UsersByRole.Single(r => r.Role == AppRoles.Client);
        Assert.AreEqual(2, clients.Active);
        Assert.AreEqual(1, clients.Suspended);
        Assert.AreEqual(1, model.UsersByRole.Single(r => r.Role == AppRoles.Trainer).PendingApproval);
        Assert.AreEqual(6, model.TotalUsers);
        Assert.AreEqual(5, model.NewRegistrationsThisMonth);
        Assert.AreEqual(1, model.ActiveUsersLast7Days);
        Assert.AreEqual(1, model.TrainersAwaitingApproval);
        CollectionAssert.AreEqual(new[] { "Una User" }, model.UnassignedClients.Select(c => c.FullName).ToArray());
        Assert.AreEqual(1, model.TrainerLoad.Single().ActiveClients);

        Assert.HasCount(14, model.LoginsPerDay.Labels);
        CollectionAssert.AreEqual(new decimal?[] { 1m, 3m }, model.LoginsPerDay.Datasets[0].Data.TakeLast(2).ToArray());
        Assert.AreEqual(4m, model.LoginsPerDay.Datasets[0].Data.Sum());
        var statuses = Enum.GetValues<AppointmentStatus>().ToList();
        var counts = model.AppointmentsByStatus.Datasets[0].Data;
        Assert.AreEqual(1m, counts[statuses.IndexOf(AppointmentStatus.Completed)]);
        Assert.AreEqual(1m, counts[statuses.IndexOf(AppointmentStatus.Pending)]);
        Assert.HasCount(5, model.RecentAuditEvents);
    }

    /// <summary>
    /// On the seeded demo data (Phase 8): trainer@'s dashboard flags client3@ and has client@'s request to answer; the
    /// admin dashboard shows the two unassigned clients and the physiotherapist awaiting approval.
    /// </summary>
    [TestMethod]
    public async Task Dashboards_SeededDemoData_ShowTheDemoScript()
    {
        await using var host = await TestHost.CreateAsync(new DateTime(2026, 10, 25, 14, 0, 0));
        await DbSeeder.SeedAsync(host.Services);
        var users = await host.Db.Users.ToDictionaryAsync(u => u.Email!, u => u.Id);
        var dashboards = host.Get<IDashboardService>();

        var trainer = (await dashboards.GetTrainerDashboardAsync(users["trainer@healthtrack.test"]))!;
        var admin = await dashboards.GetAdminDashboardAsync(users["admin@healthtrack.test"]);
        var client = (await dashboards.GetClientDashboardAsync(users["client@healthtrack.test"]))!;

        Assert.AreEqual("Dilini Silva", trainer.Clients.Single(c => c.IsInactive).FullName);
        Assert.AreEqual("Amaya Perera", trainer.PendingRequests.Single().ClientName);
        Assert.AreEqual("Ravi Fernando", trainer.TodaysSchedule.Single().ClientName);
        CollectionAssert.AreEquivalent(new[] { "Priya Nair", "Alex Morgan" }, admin.UnassignedClients.Select(c => c.FullName).ToArray());
        Assert.AreEqual(1, admin.TrainersAwaitingApproval);
        Assert.IsGreaterThan(0m, admin.LoginsPerDay.Datasets[0].Data.Sum() ?? 0m);
        Assert.IsTrue(client.WeightChangeKg < -2m);
        Assert.IsTrue(client.UpcomingAppointments.Count > 0 && client.LatestFeedback is not null && client.TodaysMeals.Count == 4);
    }
}
