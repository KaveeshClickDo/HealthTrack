using HealthTrack.Data;
using HealthTrack.Models.Entities;
using HealthTrack.Security;
using HealthTrack.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace HealthTrack.Tests.Data;

/// <summary>
/// The SQL Server model matches the database rules of spec 2.9–2.11. The model is built without opening a connection.
/// </summary>
[TestClass]
public sealed class ApplicationDbContextModelTests
{
    private static readonly Lazy<IModel> DesignTimeModel = new(() =>
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=HealthTrackModelOnly")
            .Options;
        using var db = new ApplicationDbContext(options, new FakeEncryptionService());

        // The design-time model keeps the check constraints that migrations use.
        return db.GetService<IDesignTimeModel>().Model;
    });

    private static List<IEntityType> HealthTrackEntities() => DesignTimeModel.Value.GetEntityTypes()
        .Where(e => e.ClrType.Namespace == typeof(ClientProfile).Namespace && e.ClrType != typeof(ApplicationUser))
        .ToList();

    /// <summary>The 20 HealthTrack tables are named after their classes.</summary>
    [TestMethod]
    public void OnModelCreating_HealthTrackEntities_TableNamesEqualClassNames()
    {
        var entities = HealthTrackEntities();

        Assert.HasCount(20, entities);
        foreach (var entity in entities)
        {
            Assert.AreEqual(entity.ClrType.Name, entity.GetTableName());
        }
    }

    /// <summary>R3–R32: only the seven compositions cascade; every other foreign key is Restrict (NO ACTION).</summary>
    [TestMethod]
    public void OnModelCreating_Relationships_OnlyCompositionsCascade()
    {
        var foreignKeys = HealthTrackEntities().SelectMany(e => e.GetDeclaredForeignKeys()).ToList();

        Assert.HasCount(30, foreignKeys);
        var cascading = foreignKeys.Where(fk => fk.DeleteBehavior == DeleteBehavior.Cascade)
            .Select(fk => fk.DeclaringEntityType.ClrType.Name).Order().ToArray();
        CollectionAssert.AreEqual(
            new[] { "DietPlanMeal", "MealLogItem", "Notification", "NotificationPreference", "TrainerAvailability", "WorkoutLogEntry", "WorkoutPlanExercise" },
            cascading);
        Assert.IsTrue(foreignKeys.Where(fk => fk.DeleteBehavior != DeleteBehavior.Cascade).All(fk => fk.DeleteBehavior == DeleteBehavior.Restrict));
    }

    /// <summary>The six check constraints of spec 2.11 are configured.</summary>
    [TestMethod]
    public void OnModelCreating_CheckConstraints_AllSixConfigured()
    {
        var names = HealthTrackEntities().SelectMany(e => e.GetCheckConstraints()).Select(c => c.ModelName).Order().ToArray();

        CollectionAssert.AreEqual(
            new[]
            {
                "CK_DietPlan_EndDate", "CK_HealthMeasurement_DiastolicBelowSystolic", "CK_TrainerAssignment_EndDate",
                "CK_TrainerAvailability_EndAfterStart", "CK_WorkoutPlan_EndDate", "CK_WorkoutPlan_TemplateHasNoClient"
            },
            names);
    }

    /// <summary>BR-20: exactly the six sensitive columns go through the encryption converter.</summary>
    [TestMethod]
    public void OnModelCreating_EncryptedColumns_UseEncryptedStringConverter()
    {
        var encrypted = HealthTrackEntities().SelectMany(e => e.GetProperties())
            .Where(p => p.GetValueConverter() is EncryptedStringConverter)
            .Select(p => $"{p.DeclaringType.ClrType.Name}.{p.Name}").Order().ToArray();

        CollectionAssert.AreEqual(
            new[]
            {
                "Appointment.ClientReason", "Appointment.ProfessionalNotes", "ClientProfile.Allergies",
                "ClientProfile.CurrentMedications", "ClientProfile.MedicalConditions", "HealthMeasurement.Notes"
            },
            encrypted);
    }

    /// <summary>BR-19: ReminderKey is unique where it is set.</summary>
    [TestMethod]
    public void OnModelCreating_ReminderKeyIndex_IsUniqueAndFiltered()
    {
        var index = DesignTimeModel.Value.FindEntityType(typeof(Notification))!.GetIndexes()
            .Single(i => i.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(Notification.ReminderKey) }));

        Assert.IsTrue(index.IsUnique);
        Assert.AreEqual("[ReminderKey] IS NOT NULL", index.GetFilter());
    }

    /// <summary>Appointment has a rowversion concurrency token.</summary>
    [TestMethod]
    public void OnModelCreating_AppointmentRowVersion_IsConcurrencyToken()
    {
        var rowVersion = DesignTimeModel.Value.FindEntityType(typeof(Appointment))!.FindProperty(nameof(Appointment.RowVersion))!;

        Assert.IsTrue(rowVersion.IsConcurrencyToken);
        Assert.AreEqual("rowversion", rowVersion.GetColumnType());
    }
}
