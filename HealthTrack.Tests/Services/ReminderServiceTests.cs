using HealthTrack.Models.Entities;
using HealthTrack.Models.Enums;
using HealthTrack.Services;
using HealthTrack.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace HealthTrack.Tests.Services;

/// <summary>
/// Workout, meal and appointment reminders (spec 1.8, BR-19, UC34): created when due, once each, as the user's settings
/// allow. The test clock reads Sunday 25 October 2026, 09:00.
/// </summary>
[TestClass]
public sealed class ReminderServiceTests
{
    private static readonly DateTime Sunday = new(2026, 10, 25);

    private static async Task<(TestHost Host, TrainerProfile Trainer, ClientProfile Client)> ArrangeAsync(DateTime? now = null,
        IInterceptor? interceptor = null)
    {
        var host = await TestHost.CreateAsync(now, interceptor);
        var trainer = await host.CreateTrainerAsync("trainer@example.test", firstName: "Chris");
        var client = await host.CreateClientAsync("client@example.test", firstName: "Amaya");
        return (host, trainer, client);
    }

    private static async Task<WorkoutPlan> AddWorkoutPlanAsync(TestHost host, TrainerProfile trainer, ClientProfile client, DayOfWeek day)
    {
        var exercise = await host.AddExerciseAsync("Squat " + client.ClientId);
        return await host.AddPlanAsync(trainer, client, PlanStatus.Active,
            new WorkoutPlanExercise { ExerciseId = exercise.ExerciseId, DayOfWeek = day, Sets = 3, Reps = 10 });
    }

    private static async Task<DietPlanMeal> AddLunchAsync(TestHost host, TrainerProfile trainer, ClientProfile client, int hour = 12)
    {
        var plan = await host.AddDietPlanAsync(trainer, client, PlanStatus.Active, meals: [TestHost.Meal(MealType.Lunch, hour: hour)]);
        return plan.Meals.Single();
    }

    private static Task<string[]> KeysAsync(TestHost host) =>
        host.Db.Notifications.AsNoTracking().OrderBy(n => n.ReminderKey).Select(n => n.ReminderKey!).ToArrayAsync();

    /// <summary>
    /// A training day, after the 07:00 reminder time and nothing logged: one reminder, and later runs (poll, dashboard,
    /// background service) create no second one (BR-19).
    /// </summary>
    [TestMethod]
    public async Task GenerateDueRemindersAsync_TrainingDayNothingLogged_CreatesOneReminderOnce()
    {
        var (host, trainer, client) = await ArrangeAsync();
        await using var _ = host;
        await AddWorkoutPlanAsync(host, trainer, client, DayOfWeek.Sunday);
        var reminders = host.Get<IReminderService>();

        Assert.AreEqual(1, await reminders.GenerateDueRemindersAsync(client.UserId, host.Clock.Now));
        Assert.AreEqual(0, await reminders.GenerateDueRemindersAsync(client.UserId, host.Clock.Now.AddMinutes(1)));
        Assert.AreEqual(0, await reminders.GenerateAllDueRemindersAsync(host.Clock.Now.AddHours(3)));

        var reminder = await host.Db.Notifications.SingleAsync();
        Assert.AreEqual(client.UserId, reminder.UserId);
        Assert.AreEqual(NotificationType.WorkoutReminder, reminder.Type);
        Assert.AreEqual($"WORKOUT-{client.ClientId}-20261025", reminder.ReminderKey);
        Assert.AreEqual("/Client/Workouts/Today", reminder.LinkUrl);
    }

    /// <summary>The workout reminder waits for the user's reminder time.</summary>
    [TestMethod]
    public async Task GenerateDueRemindersAsync_BeforeWorkoutReminderTime_WaitsUntilThen()
    {
        var (host, trainer, client) = await ArrangeAsync();
        await using var _ = host;
        await AddWorkoutPlanAsync(host, trainer, client, DayOfWeek.Sunday);
        await host.SetPreferencesAsync(client.UserId, p => p.WorkoutReminderTime = new TimeSpan(10, 0, 0));
        var reminders = host.Get<IReminderService>();

        Assert.AreEqual(0, await reminders.GenerateDueRemindersAsync(client.UserId, Sunday.AddHours(9).AddMinutes(59)));
        Assert.AreEqual(1, await reminders.GenerateDueRemindersAsync(client.UserId, Sunday.AddHours(10)));
    }

    /// <summary>No workout reminder once today's workout is logged, or on a rest day.</summary>
    [TestMethod]
    public async Task GenerateAllDueRemindersAsync_LoggedTodayOrRestDay_NoWorkoutReminder()
    {
        var (host, trainer, client) = await ArrangeAsync();
        await using var _ = host;
        var resting = await host.CreateClientAsync("resting@example.test");
        var plan = await AddWorkoutPlanAsync(host, trainer, client, DayOfWeek.Sunday);
        await AddWorkoutPlanAsync(host, trainer, resting, DayOfWeek.Monday);
        host.Db.WorkoutLogs.Add(new WorkoutLog
        {
            ClientId = client.ClientId,
            WorkoutPlanId = plan.WorkoutPlanId,
            WorkoutDate = Sunday,
            CreatedAt = host.Clock.Now
        });
        await host.Db.SaveChangesAsync();

        Assert.AreEqual(0, await host.Get<IReminderService>().GenerateAllDueRemindersAsync(host.Clock.Now));
        Assert.IsFalse(await host.Db.Notifications.AnyAsync());
    }

    /// <summary>A 12:00 lunch with the default 15-minute lead: reminded from 11:45 until 12:00, not before or after.</summary>
    [TestMethod]
    [DataRow(11, 40, false)]
    [DataRow(11, 45, true)]
    [DataRow(12, 0, true)]
    [DataRow(12, 1, false)]
    public async Task GenerateDueRemindersAsync_MealLeadTime_OnlyInsideTheLeadTime(int hour, int minute, bool expected)
    {
        var (host, trainer, client) = await ArrangeAsync(Sunday.AddHours(hour).AddMinutes(minute));
        await using var _ = host;
        var lunch = await AddLunchAsync(host, trainer, client);

        var created = await host.Get<IReminderService>().GenerateDueRemindersAsync(client.UserId, host.Clock.Now);

        Assert.AreEqual(expected ? 1 : 0, created);
        if (expected)
        {
            var reminder = await host.Db.Notifications.SingleAsync();
            Assert.AreEqual(NotificationType.MealReminder, reminder.Type);
            Assert.AreEqual($"MEAL-{lunch.DietPlanMealId}-20261025", reminder.ReminderKey);
            Assert.AreEqual($"/Client/Diet/LogMeal?dietPlanMealId={lunch.DietPlanMealId}", reminder.LinkUrl);
        }
    }

    /// <summary>A lunch already logged today (even without the plan meal chosen) needs no reminder.</summary>
    [TestMethod]
    public async Task GenerateDueRemindersAsync_MealTypeLoggedToday_NoMealReminder()
    {
        var (host, trainer, client) = await ArrangeAsync(Sunday.AddHours(11).AddMinutes(50));
        await using var _ = host;
        await AddLunchAsync(host, trainer, client);
        host.Db.MealLogs.Add(new MealLog { ClientId = client.ClientId, MealDate = Sunday, MealType = MealType.Lunch, CreatedAt = host.Clock.Now });
        await host.Db.SaveChangesAsync();

        Assert.AreEqual(0, await host.Get<IReminderService>().GenerateDueRemindersAsync(client.UserId, host.Clock.Now));
    }

    /// <summary>
    /// An approved appointment 23 hours ahead (inside the default 24): one reminder for the client and one for the
    /// professional, each with its own link, and none again on the next run.
    /// </summary>
    [TestMethod]
    public async Task GenerateAllDueRemindersAsync_ApprovedAppointmentInsideLeadTime_RemindsBothOnce()
    {
        var (host, trainer, client) = await ArrangeAsync();
        await using var _ = host;
        var appointment = await host.AddAppointmentAsync(client, trainer, Sunday.AddDays(1).AddHours(8));
        var reminders = host.Get<IReminderService>();

        Assert.AreEqual(2, await reminders.GenerateAllDueRemindersAsync(host.Clock.Now));
        Assert.AreEqual(0, await reminders.GenerateAllDueRemindersAsync(host.Clock.Now.AddMinutes(1)));

        var id = appointment.AppointmentId;
        var saved = await host.Db.Notifications.AsNoTracking().ToListAsync();
        var forClient = saved.Single(n => n.UserId == client.UserId);
        var forTrainer = saved.Single(n => n.UserId == trainer.UserId);
        Assert.AreEqual($"APPT-{id}-C-202610260800", forClient.ReminderKey);
        Assert.AreEqual($"/Client/Appointments/Details/{id}", forClient.LinkUrl);
        Assert.AreEqual($"APPT-{id}-T-202610260800", forTrainer.ReminderKey);
        Assert.AreEqual($"/Trainer/Appointments/Details/{id}", forTrainer.LinkUrl);
        Assert.IsTrue(saved.All(n => n.Type == NotificationType.AppointmentReminder));
    }

    /// <summary>
    /// Each person's own lead time counts (48 hours ahead: the client with 72 is reminded, the professional with the
    /// default 24 not yet), and a pending request gets no reminder.
    /// </summary>
    [TestMethod]
    public async Task GenerateAllDueRemindersAsync_LeadTimesAndStatus_FollowEachPerson()
    {
        var (host, trainer, client) = await ArrangeAsync();
        await using var _ = host;
        var other = await host.CreateClientAsync("other@example.test");
        var confirmed = await host.AddAppointmentAsync(client, trainer, Sunday.AddDays(2).AddHours(9));
        await host.AddAppointmentAsync(other, trainer, Sunday.AddDays(1).AddHours(8), status: AppointmentStatus.Pending);
        await host.SetPreferencesAsync(client.UserId, p => p.AppointmentReminderLeadHours = 72);

        Assert.AreEqual(1, await host.Get<IReminderService>().GenerateAllDueRemindersAsync(host.Clock.Now));

        CollectionAssert.AreEqual(new[] { $"APPT-{confirmed.AppointmentId}-C-202610270900" }, await KeysAsync(host));
    }

    /// <summary>The start time is part of the key, so a rescheduled appointment gets fresh reminders.</summary>
    [TestMethod]
    public async Task GenerateAllDueRemindersAsync_AppointmentRescheduled_FreshReminders()
    {
        var (host, trainer, client) = await ArrangeAsync();
        await using var _ = host;
        var appointment = await host.AddAppointmentAsync(client, trainer, Sunday.AddDays(1).AddHours(8));
        var reminders = host.Get<IReminderService>();
        await reminders.GenerateAllDueRemindersAsync(host.Clock.Now);

        appointment.Reschedule(Sunday.AddDays(1).AddHours(7), 60);
        await host.Db.SaveChangesAsync();

        Assert.AreEqual(2, await reminders.GenerateAllDueRemindersAsync(host.Clock.Now.AddMinutes(1)));
        Assert.AreEqual(4, await host.Db.Notifications.CountAsync());
        CollectionAssert.Contains(await KeysAsync(host), $"APPT-{appointment.AppointmentId}-C-202610260700");
    }

    /// <summary>BR-19: with every reminder type switched off, nothing is created even though all three are due.</summary>
    [TestMethod]
    public async Task GenerateAllDueRemindersAsync_RemindersSwitchedOff_CreatesNothing()
    {
        var (host, trainer, client) = await ArrangeAsync();
        await using var _ = host;
        await AddWorkoutPlanAsync(host, trainer, client, DayOfWeek.Sunday);
        await AddLunchAsync(host, trainer, client, hour: 9);
        await host.AddAppointmentAsync(client, trainer, Sunday.AddDays(1).AddHours(8));
        await host.SetPreferencesAsync(client.UserId, p =>
        {
            p.WorkoutRemindersEnabled = false;
            p.MealRemindersEnabled = false;
            p.AppointmentRemindersEnabled = false;
        });
        await host.SetPreferencesAsync(trainer.UserId, p => p.AppointmentRemindersEnabled = false);

        Assert.AreEqual(0, await host.Get<IReminderService>().GenerateAllDueRemindersAsync(host.Clock.Now));
        Assert.IsFalse(await host.Db.Notifications.AnyAsync());
    }

    /// <summary>The poll's check for one user creates only that user's reminders; the other person's come later.</summary>
    [TestMethod]
    public async Task GenerateDueRemindersAsync_OneUser_OnlyThatUsersReminders()
    {
        var (host, trainer, client) = await ArrangeAsync();
        await using var _ = host;
        var appointment = await host.AddAppointmentAsync(client, trainer, Sunday.AddDays(1).AddHours(8));

        Assert.AreEqual(1, await host.Get<IReminderService>().GenerateDueRemindersAsync(trainer.UserId, host.Clock.Now));

        CollectionAssert.AreEqual(new[] { $"APPT-{appointment.AppointmentId}-T-202610260800" }, await KeysAsync(host));
    }

    /// <summary>Suspended accounts get no reminders; the other person still does.</summary>
    [TestMethod]
    public async Task GenerateAllDueRemindersAsync_SuspendedClient_OnlyTheProfessionalIsReminded()
    {
        var (host, trainer, client) = await ArrangeAsync();
        await using var _ = host;
        await AddWorkoutPlanAsync(host, trainer, client, DayOfWeek.Sunday);
        var appointment = await host.AddAppointmentAsync(client, trainer, Sunday.AddDays(1).AddHours(8));
        client.User.AccountStatus = AccountStatus.Suspended;
        await host.Db.SaveChangesAsync();

        Assert.AreEqual(1, await host.Get<IReminderService>().GenerateAllDueRemindersAsync(host.Clock.Now));

        CollectionAssert.AreEqual(new[] { $"APPT-{appointment.AppointmentId}-T-202610260800" }, await KeysAsync(host));
    }

    /// <summary>Reminders go through NotifyAsync, so a user with email copies on gets the reminder by email too.</summary>
    [TestMethod]
    public async Task GenerateDueRemindersAsync_EmailCopiesOn_EmailsTheReminder()
    {
        var (host, trainer, client) = await ArrangeAsync();
        await using var _ = host;
        await AddWorkoutPlanAsync(host, trainer, client, DayOfWeek.Sunday);
        await host.SetPreferencesAsync(client.UserId, p => p.EmailEnabled = true);

        await host.Get<IReminderService>().GenerateDueRemindersAsync(client.UserId, host.Clock.Now);

        Assert.AreEqual("client@example.test", host.Email.Sent.Single().To);
        Assert.IsNotNull((await host.Db.Notifications.SingleAsync()).EmailSentAt);
    }

    /// <summary>
    /// BR-19: the background service saves the same reminder between this check and this insert. SQL Server's unique
    /// index refuses the second row; the run carries on without an error and leaves nothing half-saved behind.
    /// </summary>
    [TestMethod]
    public async Task GenerateDueRemindersAsync_OtherRunSavedTheSameKeyFirst_SkipsQuietly()
    {
        var duplicate = new SqlErrorInterceptor<Notification>(2601);
        var (host, trainer, client) = await ArrangeAsync(interceptor: duplicate);
        await using var _ = host;
        await AddWorkoutPlanAsync(host, trainer, client, DayOfWeek.Sunday);
        duplicate.FailuresLeft = 1;
        duplicate.OtherRequest = async () =>
        {
            await using var scope = host.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IReminderService>().GenerateAllDueRemindersAsync(host.Clock.Now);
        };

        var created = await host.Get<IReminderService>().GenerateDueRemindersAsync(client.UserId, host.Clock.Now);

        Assert.AreEqual(0, created);
        Assert.AreEqual(1, duplicate.Failures);
        Assert.AreEqual(1, await host.Db.Notifications.CountAsync());
        Assert.IsFalse(host.Db.ChangeTracker.Entries<Notification>().Any(e => e.State == EntityState.Added));
    }
}
