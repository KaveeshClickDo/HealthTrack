using System.Text.Json;
using HealthTrack.Models.Entities;
using HealthTrack.Models.Enums;
using HealthTrack.Services;
using HealthTrack.Tests.TestSupport;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Tests.Services;

/// <summary>
/// UC16 and BR-22: a client downloads all their data, or deletes the account (health records removed, user anonymised).
/// </summary>
[TestClass]
public sealed class AccountServicePrivacyTests
{
    // A client with something in every table the export reads and the deletion empties, plus a bystander who must be left alone.
    private static async Task<(TestHost Host, ClientProfile Client, ClientProfile Other, TrainerProfile Trainer)> ArrangeAsync()
    {
        var host = await TestHost.CreateAsync();
        var trainer = await host.CreateTrainerAsync("trainer@example.test", firstName: "Chris");
        var client = await host.CreateClientAsync("client@example.test", "Amaya");
        var other = await host.CreateClientAsync("other@example.test", "Ravi");
        client.MedicalConditions = "Asthma";
        client.Allergies = "Peanuts";
        client.User.PhoneNumber = "+94 77 123 4567";
        client.EmergencyContactName = "Zoë Perera";
        await host.AssignAsync(trainer, client);
        await host.AssignAsync(trainer, other);
        await host.SetPreferencesAsync(client.UserId, p => p.EmailEnabled = true);

        var squat = await host.AddExerciseAsync("Squat");
        var rice = await host.AddFoodAsync("Rice", 200m);
        foreach (var person in new[] { client, other })
        {
            var plan = await host.AddPlanAsync(trainer, person, PlanStatus.Active,
                new WorkoutPlanExercise { ExerciseId = squat.ExerciseId, DayOfWeek = DayOfWeek.Monday, Sets = 3, Reps = 10 });
            var diet = await host.AddDietPlanAsync(trainer, person, PlanStatus.Active, meals: [TestHost.Meal(MealType.Lunch)]);
            await host.AddMeasurementAsync(person, 70m, host.Clock.Today.AddDays(-3));
            await host.AddAppointmentAsync(person, trainer, host.Clock.Today.AddDays(2).AddHours(10));

            var log = new WorkoutLog { ClientId = person.ClientId, WorkoutPlanId = plan.WorkoutPlanId, WorkoutDate = host.Clock.Today, CreatedAt = host.Clock.Now };
            log.AddEntry(squat, 70m, 30);
            var meal = new MealLog { ClientId = person.ClientId, DietPlanMealId = diet.Meals.Single().DietPlanMealId, MealDate = host.Clock.Today, MealType = MealType.Lunch, CreatedAt = host.Clock.Now };
            meal.AddItem(rice, 2m);
            host.Db.WorkoutLogs.Add(log);
            host.Db.MealLogs.Add(meal);
            host.Db.Feedback.Add(new Feedback { TrainerId = trainer.TrainerId, ClientId = person.ClientId, WorkoutLog = log, Category = FeedbackCategory.Workout, Message = "Nice squats", Rating = 4, CreatedAt = host.Clock.Now });
            host.Db.Notifications.Add(new Notification { UserId = person.UserId, Type = NotificationType.System, Title = "Hello", Message = "Welcome", CreatedAt = host.Clock.Now });
        }

        host.Db.AuditLogs.Add(new AuditLog { UserId = client.UserId, Action = AuditAction.Login, Timestamp = host.Clock.Now.AddHours(-1) });
        await host.Db.SaveChangesAsync();
        return (host, client, other, trainer);
    }

    /// <summary>
    /// The JSON holds the account, the medical details decrypted, every kind of record and the notifications; enums are
    /// names; the export is audited without any of its contents.
    /// </summary>
    [TestMethod]
    public async Task ExportPersonalDataAsync_Client_ContainsEverythingAndIsAudited()
    {
        var (host, client, _, _) = await ArrangeAsync();
        await using var _ = host;

        var export = (await host.Get<IAccountService>().ExportPersonalDataAsync(client.UserId))!;

        Assert.AreEqual("healthtrack-my-data-20261025.json", export.FileName);
        using var json = JsonDocument.Parse(export.Content);
        var root = json.RootElement;
        Assert.AreEqual("client@example.test", root.GetProperty("account").GetProperty("email").GetString());
        // Readable in a text editor: + and accented letters aren't turned into \u escapes.
        var text = System.Text.Encoding.UTF8.GetString(export.Content);
        Assert.Contains("\"+94 77 123 4567\"", text);
        Assert.Contains("Zoë Perera", text);
        Assert.AreEqual("Asthma", root.GetProperty("healthProfile").GetProperty("medicalConditions").GetString());
        Assert.AreEqual("LoseWeight", root.GetProperty("healthProfile").GetProperty("fitnessGoal").GetString());
        Assert.IsTrue(root.GetProperty("notificationSettings").GetProperty("emailEnabled").GetBoolean());
        Assert.AreEqual(1, root.GetProperty("measurements").GetArrayLength());
        Assert.AreEqual("Squat", root.GetProperty("workoutPlans")[0].GetProperty("exercises")[0].GetProperty("exercise").GetString());
        Assert.AreEqual(1, root.GetProperty("dietPlans")[0].GetProperty("meals").GetArrayLength());
        Assert.AreEqual(175m, root.GetProperty("workoutLogs")[0].GetProperty("entries")[0].GetProperty("caloriesBurned").GetDecimal());
        Assert.AreEqual("Rice", root.GetProperty("mealLogs")[0].GetProperty("items")[0].GetProperty("food").GetString());
        Assert.AreEqual("Knee feels stiff", root.GetProperty("appointments")[0].GetProperty("clientReason").GetString());
        Assert.AreEqual("Nice squats", root.GetProperty("feedback")[0].GetProperty("message").GetString());
        Assert.AreEqual(1, root.GetProperty("notifications").GetArrayLength());
        Assert.AreEqual(1, root.GetProperty("trainerAssignments").GetArrayLength());

        var audit = await host.Db.AuditLogs.SingleAsync(a => a.Action == AuditAction.Export);
        Assert.AreEqual(client.UserId, audit.UserId);
        Assert.DoesNotContain("Asthma", audit.Details!);
    }

    /// <summary>Only clients have personal health data to export.</summary>
    [TestMethod]
    public async Task ExportPersonalDataAsync_TrainerOrUnknownUser_ReturnsNull()
    {
        var (host, _, _, trainer) = await ArrangeAsync();
        await using var _ = host;
        var accounts = host.Get<IAccountService>();

        Assert.IsNull(await accounts.ExportPersonalDataAsync(trainer.UserId));
        Assert.IsNull(await accounts.ExportPersonalDataAsync("no-such-user"));
    }

    /// <summary>
    /// BR-22: with the right password every health record of the client goes, the user row is anonymised and locked for
    /// good, the audit log stays, and nobody else's data is touched.
    /// </summary>
    [TestMethod]
    public async Task DeleteAccountAsync_RightPassword_RemovesHealthRowsAndAnonymises()
    {
        var (host, client, other, _) = await ArrangeAsync();
        await using var _ = host;
        var userId = client.UserId;
        var clientId = client.ClientId;
        var oldStamp = client.User.SecurityStamp;
        var auditBefore = await host.Db.AuditLogs.CountAsync(a => a.UserId == userId);
        var accounts = host.Get<IAccountService>();

        Assert.AreEqual(DeleteAccountStatus.Deleted, await accounts.DeleteAccountAsync(userId, TestHost.Password));

        var db = host.Db;
        Assert.IsFalse(await db.ClientProfiles.AnyAsync(c => c.ClientId == clientId));
        Assert.IsFalse(await db.HealthMeasurements.AnyAsync(m => m.ClientId == clientId));
        Assert.IsFalse(await db.WorkoutLogs.AnyAsync(l => l.ClientId == clientId));
        Assert.IsFalse(await db.MealLogs.AnyAsync(l => l.ClientId == clientId));
        Assert.IsFalse(await db.WorkoutPlans.AnyAsync(p => p.ClientId == clientId));
        Assert.IsFalse(await db.DietPlans.AnyAsync(p => p.ClientId == clientId));
        Assert.IsFalse(await db.Appointments.AnyAsync(a => a.ClientId == clientId));
        Assert.IsFalse(await db.Feedback.AnyAsync(f => f.ClientId == clientId));
        Assert.IsFalse(await db.TrainerAssignments.AnyAsync(a => a.ClientId == clientId));
        Assert.IsFalse(await db.Notifications.AnyAsync(n => n.UserId == userId));
        Assert.IsFalse(await db.NotificationPreferences.AnyAsync(p => p.UserId == userId));
        // Composition children went with their parents.
        Assert.AreEqual(1, await db.WorkoutLogEntries.CountAsync());
        Assert.AreEqual(1, await db.MealLogItems.CountAsync());
        Assert.AreEqual(1, await db.WorkoutPlanExercises.CountAsync());
        Assert.AreEqual(1, await db.DietPlanMeals.CountAsync());

        var user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == userId);
        Assert.AreEqual("Deleted user", user.FullName);
        Assert.AreEqual($"deleted-{userId}@healthtrack.invalid", user.Email);
        Assert.AreEqual(user.Email, user.UserName);
        Assert.IsNull(user.PhoneNumber);
        Assert.IsNull(user.PasswordHash);
        Assert.AreEqual(AccountStatus.Deactivated, user.AccountStatus);
        Assert.AreEqual(DateTimeOffset.MaxValue, user.LockoutEnd);
        Assert.AreNotEqual(oldStamp, user.SecurityStamp);

        // The audit trail stays, with the deletion recorded.
        Assert.AreEqual(auditBefore + 1, await db.AuditLogs.CountAsync(a => a.UserId == userId));
        Assert.AreEqual(AuditAction.Delete, (await db.AuditLogs.OrderBy(a => a.AuditLogId).LastAsync()).Action);

        // Nobody else's data is touched, and the old login no longer works.
        Assert.IsTrue(await db.ClientProfiles.AnyAsync(c => c.ClientId == other.ClientId));
        Assert.AreEqual(1, await db.WorkoutLogs.CountAsync(l => l.ClientId == other.ClientId));
        Assert.AreEqual(1, await db.Appointments.CountAsync(a => a.ClientId == other.ClientId));
        Assert.AreEqual(LoginStatus.InvalidCredentials, (await accounts.LoginAsync("client@example.test", TestHost.Password)).Status);
    }

    /// <summary>A wrong password deletes nothing and counts towards the lockout, like a failed sign-in.</summary>
    [TestMethod]
    public async Task DeleteAccountAsync_WrongPassword_ChangesNothing()
    {
        var (host, client, _, _) = await ArrangeAsync();
        await using var _ = host;
        var accounts = host.Get<IAccountService>();

        Assert.AreEqual(DeleteAccountStatus.WrongPassword, await accounts.DeleteAccountAsync(client.UserId, "Wrong-passw0rd"));

        Assert.IsTrue(await host.Db.ClientProfiles.AnyAsync(c => c.ClientId == client.ClientId));
        Assert.AreEqual(1, await host.Db.HealthMeasurements.CountAsync(m => m.ClientId == client.ClientId));
        var user = await host.Get<UserManager<ApplicationUser>>().FindByIdAsync(client.UserId);
        Assert.AreEqual("client@example.test", user!.Email);
        Assert.AreEqual(1, user.AccessFailedCount);
        Assert.IsFalse(await host.Db.AuditLogs.AnyAsync(a => a.Action == AuditAction.Delete));
    }

    /// <summary>Guessing the password on the deletion form locks the account after 5 tries, as sign-in does.</summary>
    [TestMethod]
    public async Task DeleteAccountAsync_FiveWrongPasswords_LockedOut()
    {
        var (host, client, _, _) = await ArrangeAsync();
        await using var _ = host;
        var accounts = host.Get<IAccountService>();

        for (var attempt = 1; attempt < 5; attempt++)
        {
            Assert.AreEqual(DeleteAccountStatus.WrongPassword, await accounts.DeleteAccountAsync(client.UserId, "Wrong-passw0rd"));
        }

        Assert.AreEqual(DeleteAccountStatus.LockedOut, await accounts.DeleteAccountAsync(client.UserId, "Wrong-passw0rd"));
        Assert.AreEqual(DeleteAccountStatus.LockedOut, await accounts.DeleteAccountAsync(client.UserId, TestHost.Password));
        Assert.IsTrue(await host.Db.ClientProfiles.AnyAsync(c => c.ClientId == client.ClientId));
    }

    /// <summary>A trainer has no client account to delete through this route.</summary>
    [TestMethod]
    public async Task DeleteAccountAsync_Trainer_NotFound()
    {
        var (host, _, _, trainer) = await ArrangeAsync();
        await using var _ = host;

        Assert.AreEqual(DeleteAccountStatus.NotFound, await host.Get<IAccountService>().DeleteAccountAsync(trainer.UserId, TestHost.Password));
    }
}
