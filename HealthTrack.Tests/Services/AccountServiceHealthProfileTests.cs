using HealthTrack.Models.Enums;
using HealthTrack.Models.ViewModels;
using HealthTrack.Services;
using HealthTrack.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Tests.Services;

/// <summary>
/// The client's health profile: goal, activity, target weight, the encrypted medical details and the sharing choice.
/// </summary>
[TestClass]
public sealed class AccountServiceHealthProfileTests
{
    /// <summary>Saving stores every field and blanks as null; the audit row records the sharing choice but no health data.</summary>
    [TestMethod]
    public async Task UpdateHealthProfileAsync_ValidForm_SavesFieldsWithoutAuditingHealthData()
    {
        await using var host = await TestHost.CreateAsync();
        var client = await host.CreateClientAsync("client@example.test");
        var accounts = host.Get<IAccountService>();

        Assert.IsTrue(await accounts.UpdateHealthProfileAsync(client.UserId, new HealthProfileViewModel
        {
            FitnessGoal = FitnessGoal.GainMuscle,
            ActivityLevel = ActivityLevel.VeryActive,
            TargetWeightKg = 78.5m,
            MedicalConditions = "Asthma",
            Allergies = "  ",
            CurrentMedications = "Inhaler",
            EmergencyContactName = "Nimal Perera",
            EmergencyContactPhone = "+94 77 123 4567",
            ShareMedicalInfoWithTrainer = false
        }));

        var form = await accounts.GetHealthProfileAsync(client.UserId);
        Assert.AreEqual(FitnessGoal.GainMuscle, form!.FitnessGoal);
        Assert.AreEqual(ActivityLevel.VeryActive, form.ActivityLevel);
        Assert.AreEqual(78.5m, form.TargetWeightKg);
        Assert.AreEqual("Asthma", form.MedicalConditions);
        Assert.IsNull(form.Allergies);
        Assert.IsFalse(form.ShareMedicalInfoWithTrainer);
        Assert.AreEqual(host.Clock.Now, (await host.Db.ClientProfiles.AsNoTracking().SingleAsync()).UpdatedAt);

        var audit = await host.Db.AuditLogs.SingleAsync();
        Assert.AreEqual(AuditAction.Update, audit.Action);
        StringAssert.Contains(audit.Details, "hidden from");
        Assert.IsFalse(audit.Details!.Contains("Asthma") || audit.Details.Contains("78.5"));
    }

    /// <summary>A user without a client profile (a trainer) has no health profile.</summary>
    [TestMethod]
    public async Task GetHealthProfileAsync_Trainer_ReturnsNull()
    {
        await using var host = await TestHost.CreateAsync();
        var trainer = await host.CreateTrainerAsync("trainer@example.test");
        var accounts = host.Get<IAccountService>();

        Assert.IsNull(await accounts.GetHealthProfileAsync(trainer.UserId));
        Assert.IsFalse(await accounts.UpdateHealthProfileAsync(trainer.UserId,
            new HealthProfileViewModel { FitnessGoal = FitnessGoal.LoseWeight, ActivityLevel = ActivityLevel.Sedentary }));
    }
}
