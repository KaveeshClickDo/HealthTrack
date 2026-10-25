using HealthTrack.Models.Entities;
using HealthTrack.Models.Enums;
using HealthTrack.Models.ViewModels;
using HealthTrack.Services;
using HealthTrack.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Tests.Services;

/// <summary>
/// BR-08 and the ownership rule of CLAUDE.md, service by service: given ids that belong to someone else, every method answers
/// nothing (null or false, which the controllers turn into 404) and changes nothing. Amaya is Chris's client; Ravi is
/// Olivia's.
/// </summary>
[TestClass]
public sealed class OwnershipTests
{
    private sealed record World(TestHost Host, TrainerProfile Chris, TrainerProfile Olivia, ClientProfile Amaya, ClientProfile Ravi,
        WorkoutPlan RaviPlan, DietPlan RaviDiet, WorkoutLog RaviLog, Appointment RaviAppointment, Exercise Squat, FoodItem Rice);

    private static async Task<World> ArrangeAsync()
    {
        var host = await TestHost.CreateAsync();
        var chris = await host.CreateTrainerAsync("chris@example.test", firstName: "Chris");
        var olivia = await host.CreateTrainerAsync("olivia@example.test", firstName: "Olivia");
        var amaya = await host.CreateClientAsync("amaya@example.test", "Amaya");
        var ravi = await host.CreateClientAsync("ravi@example.test", "Ravi");
        await host.AssignAsync(chris, amaya);
        await host.AssignAsync(olivia, ravi);

        var squat = await host.AddExerciseAsync("Squat");
        var rice = await host.AddFoodAsync("Rice", 200m);
        var raviPlan = await host.AddPlanAsync(olivia, ravi, PlanStatus.Draft,
            new WorkoutPlanExercise { ExerciseId = squat.ExerciseId, DayOfWeek = DayOfWeek.Monday, Sets = 3, Reps = 10 });
        var raviDiet = await host.AddDietPlanAsync(olivia, ravi, PlanStatus.Draft, meals: [TestHost.Meal(MealType.Lunch)]);
        var raviLog = new WorkoutLog { ClientId = ravi.ClientId, WorkoutDate = host.Clock.Today, CreatedAt = host.Clock.Now };
        raviLog.AddEntry(squat, 75m, 30);
        host.Db.WorkoutLogs.Add(raviLog);
        await host.Db.SaveChangesAsync();
        var raviAppointment = await host.AddAppointmentAsync(ravi, olivia, host.Clock.Today.AddDays(3).AddHours(10), status: AppointmentStatus.Pending);

        return new World(host, chris, olivia, amaya, ravi, raviPlan, raviDiet, raviLog, raviAppointment, squat, rice);
    }

    /// <summary>A client can't open or cancel another client's appointment.</summary>
    [TestMethod]
    public async Task AppointmentService_OtherClientsAppointment_NothingForTheClient()
    {
        var w = await ArrangeAsync();
        await using var _ = w.Host;
        var appointments = w.Host.Get<IAppointmentService>();
        var version = Convert.ToBase64String(w.RaviAppointment.RowVersion);

        Assert.IsNull(await appointments.GetDetailsAsync(w.RaviAppointment.AppointmentId, w.Amaya.UserId));
        Assert.IsFalse(await appointments.CancelAsync(w.RaviAppointment.AppointmentId, w.Amaya.UserId, "Not mine", version));
        Assert.AreEqual(AppointmentStatus.Pending, (await w.Host.Db.Appointments.AsNoTracking().SingleAsync()).Status);
    }

    /// <summary>BR-08: a professional only reaches their own appointments.</summary>
    [TestMethod]
    public async Task AppointmentService_OtherProfessionalsAppointment_NothingForTheTrainer()
    {
        var w = await ArrangeAsync();
        await using var _ = w.Host;
        var appointments = w.Host.Get<IAppointmentService>();
        var id = w.RaviAppointment.AppointmentId;
        var version = Convert.ToBase64String(w.RaviAppointment.RowVersion);

        Assert.IsNull(await appointments.GetDetailsAsync(id, w.Chris.UserId));
        Assert.IsNull(await appointments.GetRescheduleFormAsync(id, w.Chris.UserId, null, null));
        Assert.IsFalse(await appointments.ApproveAsync(id, w.Chris.UserId, null, version));
        Assert.IsFalse(await appointments.RejectAsync(id, w.Chris.UserId, "Busy", version));
        Assert.IsFalse(await appointments.CompleteAsync(id, w.Chris.UserId, "Notes", version));
        Assert.AreEqual(AppointmentStatus.Pending, (await w.Host.Db.Appointments.AsNoTracking().SingleAsync()).Status);
    }

    /// <summary>A workout can't be logged against another client's plan.</summary>
    [TestMethod]
    public async Task WorkoutService_LogAgainstOtherClientsPlan_ReturnsNull()
    {
        var w = await ArrangeAsync();
        await using var _ = w.Host;
        var model = new LogWorkoutViewModel
        {
            WorkoutPlanId = w.RaviPlan.WorkoutPlanId,
            WorkoutDate = w.Host.Clock.Today,
            Entries = [new LogEntryViewModel { Include = true, ExerciseId = w.Squat.ExerciseId, DurationMinutes = 20 }]
        };

        Assert.IsNull(await w.Host.Get<IWorkoutService>().LogWorkoutAsync(w.Amaya.UserId, model));
        Assert.AreEqual(0, await w.Host.Db.WorkoutLogs.CountAsync(l => l.ClientId == w.Amaya.ClientId));
    }

    /// <summary>BR-08: another trainer's client plan can't be opened, activated, archived, copied, or given a template.</summary>
    [TestMethod]
    public async Task WorkoutService_OtherTrainersPlanOrClient_NothingForTheTrainer()
    {
        var w = await ArrangeAsync();
        await using var _ = w.Host;
        var workouts = w.Host.Get<IWorkoutService>();
        var planId = w.RaviPlan.WorkoutPlanId;
        var template = await w.Host.AddPlanAsync(w.Chris, null, PlanStatus.Draft,
            new WorkoutPlanExercise { ExerciseId = w.Squat.ExerciseId, DayOfWeek = DayOfWeek.Friday, Sets = 3, Reps = 8 });

        Assert.IsNull(await workouts.GetCreatePlanFormAsync(w.Chris.UserId, w.Ravi.ClientId, isTemplate: false));
        Assert.IsNotNull(await workouts.GetCreatePlanFormAsync(w.Chris.UserId, w.Amaya.ClientId, isTemplate: false));
        Assert.IsNull(await workouts.GetPlanForEditAsync(planId, w.Chris.UserId));
        Assert.IsFalse(await workouts.ActivatePlanAsync(planId, w.Chris.UserId));
        Assert.IsFalse(await workouts.ArchivePlanAsync(planId, w.Chris.UserId));
        Assert.IsNull(await workouts.SaveAsTemplateAsync(planId, w.Chris.UserId));
        Assert.IsNull(await workouts.AssignTemplateAsync(template.WorkoutPlanId, w.Ravi.ClientId, w.Host.Clock.Today, true, w.Chris.UserId));
        Assert.AreEqual(PlanStatus.Draft, (await w.Host.Db.WorkoutPlans.AsNoTracking().SingleAsync(p => p.WorkoutPlanId == planId)).Status);
        Assert.AreEqual(1, await w.Host.Db.WorkoutPlans.CountAsync(p => p.ClientId == w.Ravi.ClientId));
    }

    /// <summary>A meal can't be logged against another client's planned meal, and its form isn't shown.</summary>
    [TestMethod]
    public async Task DietService_OtherClientsPlannedMeal_NothingForTheClient()
    {
        var w = await ArrangeAsync();
        await using var _ = w.Host;
        var diet = w.Host.Get<IDietService>();
        var mealId = w.RaviDiet.Meals.Single().DietPlanMealId;
        var model = new LogMealViewModel
        {
            DietPlanMealId = mealId,
            MealDate = w.Host.Clock.Today,
            MealType = MealType.Lunch,
            Items = [new LogMealItemViewModel { FoodItemId = w.Rice.FoodItemId, Quantity = 1m }]
        };

        Assert.IsNull(await diet.GetLogMealFormAsync(w.Amaya.ClientId, mealId));
        Assert.IsNull(await diet.LogMealAsync(w.Amaya.UserId, model));
        Assert.IsFalse(await w.Host.Db.MealLogs.AnyAsync());
    }

    /// <summary>BR-08: another trainer's diet plan, or a client who isn't assigned, is out of reach.</summary>
    [TestMethod]
    public async Task DietService_OtherTrainersPlanOrClient_NothingForTheTrainer()
    {
        var w = await ArrangeAsync();
        await using var _ = w.Host;
        var diet = w.Host.Get<IDietService>();

        Assert.IsNull(await diet.GetPlanForEditAsync(w.RaviDiet.DietPlanId, w.Chris.UserId));
        Assert.IsFalse(await diet.ActivatePlanAsync(w.RaviDiet.DietPlanId, w.Chris.UserId));
        Assert.IsNull(await diet.GetCreatePlanFormAsync(w.Chris.UserId, w.Ravi.ClientId));
        Assert.AreEqual(PlanStatus.Draft, (await w.Host.Db.DietPlans.AsNoTracking().SingleAsync()).Status);
    }

    /// <summary>BR-08: no feedback for another trainer's client, nor on their session through one's own client.</summary>
    [TestMethod]
    public async Task FeedbackService_OtherTrainersClientOrSession_ReturnsNull()
    {
        var w = await ArrangeAsync();
        await using var _ = w.Host;
        var feedback = w.Host.Get<IFeedbackService>();

        Assert.IsNull(await feedback.GetFeedbackFormAsync(w.Chris.UserId, w.Ravi.ClientId, null));
        Assert.IsNull(await feedback.GetFeedbackFormAsync(w.Chris.UserId, w.Amaya.ClientId, w.RaviLog.WorkoutLogId));
        Assert.IsNull(await feedback.GiveFeedbackAsync(w.Chris.UserId,
            new FeedbackViewModel { ClientId = w.Ravi.ClientId, Category = FeedbackCategory.General, Message = "Hello" }));
        Assert.IsNull(await feedback.GiveFeedbackAsync(w.Chris.UserId,
            new FeedbackViewModel { ClientId = w.Amaya.ClientId, WorkoutLogId = w.RaviLog.WorkoutLogId, Category = FeedbackCategory.Workout, Message = "Hello" }));
        Assert.IsFalse(await w.Host.Db.Feedback.AnyAsync());
    }

    /// <summary>BR-08: a trainer can't open the progress page of a client who isn't assigned to them.</summary>
    [TestMethod]
    public async Task ProgressService_UnassignedClient_ReturnsNull()
    {
        var w = await ArrangeAsync();
        await using var _ = w.Host;
        var range = ProgressRangeViewModel.Resolve(null, null, null, w.Host.Clock.Today);

        Assert.IsNull(await w.Host.Get<IProgressService>().GetTrainerClientProgressAsync(w.Chris.UserId, w.Ravi.ClientId, range));
        Assert.IsNotNull(await w.Host.Get<IProgressService>().GetTrainerClientProgressAsync(w.Chris.UserId, w.Amaya.ClientId, range));
    }

    /// <summary>A client can't mark another user's notification as read.</summary>
    [TestMethod]
    public async Task NotificationService_OtherUsersNotification_ReturnsFalse()
    {
        var w = await ArrangeAsync();
        await using var _ = w.Host;
        var notification = await w.Host.Get<INotificationService>().NotifyAsync(w.Ravi.UserId, NotificationType.System, "For Ravi", "Message");

        Assert.IsFalse(await w.Host.Get<INotificationService>().MarkAsReadAsync(w.Amaya.UserId, notification.NotificationId));
        Assert.IsFalse((await w.Host.Db.Notifications.AsNoTracking().SingleAsync()).IsRead);
    }
}
