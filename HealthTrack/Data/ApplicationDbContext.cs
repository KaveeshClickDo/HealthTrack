using System.Data;
using HealthTrack.Models.Entities;
using HealthTrack.Security;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HealthTrack.Data
{
    /// <summary>
    /// The EF Core context for the whole database: the Identity tables plus the 20 HealthTrack tables of spec section 2.
    /// </summary>
    /// <remarks>
    /// EF Core builds the model once per context type and caches it, so the encryption converter keeps the
    /// <see cref="IEncryptionService"/> of the first context created. That is why the service is a singleton.
    /// </remarks>
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        private readonly IEncryptionService _encryption;

        /// <summary>
        /// Creates the context.
        /// </summary>
        /// <param name="options">The provider and connection settings.</param>
        /// <param name="encryption">Encrypts the six sensitive columns (BR-20).</param>
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IEncryptionService encryption)
            : base(options)
        {
            _encryption = encryption;
        }

        /// <summary>Client profiles.</summary>
        public DbSet<ClientProfile> ClientProfiles => Set<ClientProfile>();

        /// <summary>Trainer and health professional profiles.</summary>
        public DbSet<TrainerProfile> TrainerProfiles => Set<TrainerProfile>();

        /// <summary>Trainers' weekly availability windows.</summary>
        public DbSet<TrainerAvailability> TrainerAvailabilities => Set<TrainerAvailability>();

        /// <summary>Trainer–client assignments, with history.</summary>
        public DbSet<TrainerAssignment> TrainerAssignments => Set<TrainerAssignment>();

        /// <summary>The exercise library.</summary>
        public DbSet<Exercise> Exercises => Set<Exercise>();

        /// <summary>Client workout plans and routine templates.</summary>
        public DbSet<WorkoutPlan> WorkoutPlans => Set<WorkoutPlan>();

        /// <summary>Exercises prescribed in workout plans.</summary>
        public DbSet<WorkoutPlanExercise> WorkoutPlanExercises => Set<WorkoutPlanExercise>();

        /// <summary>Workout sessions logged by clients.</summary>
        public DbSet<WorkoutLog> WorkoutLogs => Set<WorkoutLog>();

        /// <summary>Exercises done in logged sessions.</summary>
        public DbSet<WorkoutLogEntry> WorkoutLogEntries => Set<WorkoutLogEntry>();

        /// <summary>The food library.</summary>
        public DbSet<FoodItem> FoodItems => Set<FoodItem>();

        /// <summary>Client diet plans.</summary>
        public DbSet<DietPlan> DietPlans => Set<DietPlan>();

        /// <summary>Meals scheduled in diet plans.</summary>
        public DbSet<DietPlanMeal> DietPlanMeals => Set<DietPlanMeal>();

        /// <summary>Meals logged by clients.</summary>
        public DbSet<MealLog> MealLogs => Set<MealLog>();

        /// <summary>Foods in logged meals.</summary>
        public DbSet<MealLogItem> MealLogItems => Set<MealLogItem>();

        /// <summary>Clients' health measurements.</summary>
        public DbSet<HealthMeasurement> HealthMeasurements => Set<HealthMeasurement>();

        /// <summary>Appointments between clients and professionals.</summary>
        public DbSet<Appointment> Appointments => Set<Appointment>();

        /// <summary>Trainer feedback to clients.</summary>
        public DbSet<Feedback> Feedback => Set<Feedback>();

        /// <summary>In-app notifications and reminders.</summary>
        public DbSet<Notification> Notifications => Set<Notification>();

        /// <summary>Users' reminder settings.</summary>
        public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();

        /// <summary>The audit trail.</summary>
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

        /// <summary>
        /// Starts a serializable transaction for booking or rescheduling an appointment, so two clients cannot take the
        /// same slot (BR-15). Returns null on a non-relational provider (EF Core InMemory in tests), which has no isolation levels.
        /// </summary>
        /// <param name="cancellationToken">Cancels the operation.</param>
        /// <returns>The transaction to commit, or null when the provider isn't relational.</returns>
        public async Task<IDbContextTransaction?> BeginSerializableTransactionAsync(CancellationToken cancellationToken = default)
        {
            if (!Database.IsRelational())
            {
                return null;
            }

            return await Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        }

        /// <summary>
        /// Maps the 20 HealthTrack entities after Identity's own configuration: table names, relationships R3–R32 with
        /// their delete rules, indexes, check constraints and the encrypted columns (spec 2.9–2.11).
        /// </summary>
        /// <param name="builder">The model builder.</param>
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            var encrypted = new EncryptedStringConverter(_encryption);

            // Delete behaviour is set per relationship: only the seven compositions cascade, everything else is
            // Restrict (NO ACTION). Identity's own tables keep their cascades, so we never loop over all foreign keys.
            ConfigureProfiles(builder, encrypted);
            ConfigureWorkout(builder);
            ConfigureDiet(builder);
            ConfigureHealth(builder, encrypted);
            ConfigureAppointments(builder, encrypted);
            ConfigureNotificationsAndAudit(builder);
        }

        private static void ConfigureProfiles(ModelBuilder builder, EncryptedStringConverter encrypted)
        {
            builder.Entity<ClientProfile>(entity =>
            {
                entity.ToTable(nameof(ClientProfile));

                // R3
                entity.HasOne(c => c.User).WithOne(u => u.ClientProfile)
                    .HasForeignKey<ClientProfile>(c => c.UserId).OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(c => c.UserId).IsUnique();

                // BR-20: medical details are encrypted at rest.
                entity.Property(c => c.MedicalConditions).HasConversion(encrypted);
                entity.Property(c => c.Allergies).HasConversion(encrypted);
                entity.Property(c => c.CurrentMedications).HasConversion(encrypted);
            });

            builder.Entity<TrainerProfile>(entity =>
            {
                entity.ToTable(nameof(TrainerProfile));

                // R4
                entity.HasOne(t => t.User).WithOne(u => u.TrainerProfile)
                    .HasForeignKey<TrainerProfile>(t => t.UserId).OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(t => t.UserId).IsUnique();
            });

            builder.Entity<TrainerAvailability>(entity =>
            {
                entity.ToTable(nameof(TrainerAvailability), table =>
                    table.HasCheckConstraint("CK_TrainerAvailability_EndAfterStart", "[EndTime] > [StartTime]"));

                // R8, composition
                entity.HasOne(a => a.Trainer).WithMany(t => t.Availabilities)
                    .HasForeignKey(a => a.TrainerId).OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(a => new { a.TrainerId, a.DayOfWeek, a.StartTime }).IsUnique();
            });

            builder.Entity<TrainerAssignment>(entity =>
            {
                entity.ToTable(nameof(TrainerAssignment), table =>
                    table.HasCheckConstraint("CK_TrainerAssignment_EndDate", "[EndDate] IS NULL OR [EndDate] >= [StartDate]"));

                // R9, R10
                entity.HasOne(a => a.Client).WithMany(c => c.Assignments)
                    .HasForeignKey(a => a.ClientId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(a => a.Trainer).WithMany(t => t.Assignments)
                    .HasForeignKey(a => a.TrainerId).OnDelete(DeleteBehavior.Restrict);
            });
        }

        private static void ConfigureWorkout(ModelBuilder builder)
        {
            builder.Entity<Exercise>(entity =>
            {
                entity.ToTable(nameof(Exercise));
                entity.HasIndex(x => x.Name).IsUnique();
            });

            builder.Entity<WorkoutPlan>(entity =>
            {
                entity.ToTable(nameof(WorkoutPlan), table =>
                {
                    // A template has no client, and a client plan is never a template.
                    table.HasCheckConstraint("CK_WorkoutPlan_TemplateHasNoClient",
                        "([IsTemplate] = 1 AND [ClientId] IS NULL) OR ([IsTemplate] = 0 AND [ClientId] IS NOT NULL)");
                    table.HasCheckConstraint("CK_WorkoutPlan_EndDate", "[EndDate] IS NULL OR [EndDate] >= [StartDate]");
                });

                // R11, R12, R13
                entity.HasOne(p => p.Trainer).WithMany(t => t.WorkoutPlans)
                    .HasForeignKey(p => p.TrainerId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(p => p.Client).WithMany(c => c.WorkoutPlans)
                    .HasForeignKey(p => p.ClientId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(p => p.SourceTemplate).WithMany(p => p.Copies)
                    .HasForeignKey(p => p.SourceTemplateId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<WorkoutPlanExercise>(entity =>
            {
                entity.ToTable(nameof(WorkoutPlanExercise));

                // R14, composition; R15
                entity.HasOne(x => x.WorkoutPlan).WithMany(p => p.Exercises)
                    .HasForeignKey(x => x.WorkoutPlanId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(x => x.Exercise).WithMany(e => e.WorkoutPlanExercises)
                    .HasForeignKey(x => x.ExerciseId).OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<WorkoutLog>(entity =>
            {
                entity.ToTable(nameof(WorkoutLog));

                // R16, R17
                entity.HasOne(l => l.Client).WithMany(c => c.WorkoutLogs)
                    .HasForeignKey(l => l.ClientId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(l => l.WorkoutPlan).WithMany(p => p.WorkoutLogs)
                    .HasForeignKey(l => l.WorkoutPlanId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(l => new { l.ClientId, l.WorkoutDate });
            });

            builder.Entity<WorkoutLogEntry>(entity =>
            {
                entity.ToTable(nameof(WorkoutLogEntry));

                // R18, composition; R19
                entity.HasOne(x => x.WorkoutLog).WithMany(l => l.Entries)
                    .HasForeignKey(x => x.WorkoutLogId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(x => x.Exercise).WithMany(e => e.WorkoutLogEntries)
                    .HasForeignKey(x => x.ExerciseId).OnDelete(DeleteBehavior.Restrict);
            });
        }

        private static void ConfigureDiet(ModelBuilder builder)
        {
            builder.Entity<FoodItem>(entity =>
            {
                entity.ToTable(nameof(FoodItem));
                entity.HasIndex(f => f.Name).IsUnique();
            });

            builder.Entity<DietPlan>(entity =>
            {
                entity.ToTable(nameof(DietPlan), table =>
                    table.HasCheckConstraint("CK_DietPlan_EndDate", "[EndDate] IS NULL OR [EndDate] >= [StartDate]"));

                // R20, R21
                entity.HasOne(p => p.Trainer).WithMany(t => t.DietPlans)
                    .HasForeignKey(p => p.TrainerId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(p => p.Client).WithMany(c => c.DietPlans)
                    .HasForeignKey(p => p.ClientId).OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<DietPlanMeal>(entity =>
            {
                entity.ToTable(nameof(DietPlanMeal));

                // R22, composition
                entity.HasOne(m => m.DietPlan).WithMany(p => p.Meals)
                    .HasForeignKey(m => m.DietPlanId).OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<MealLog>(entity =>
            {
                entity.ToTable(nameof(MealLog));

                // R23, R24
                entity.HasOne(l => l.Client).WithMany(c => c.MealLogs)
                    .HasForeignKey(l => l.ClientId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(l => l.DietPlanMeal).WithMany(m => m.MealLogs)
                    .HasForeignKey(l => l.DietPlanMealId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(l => new { l.ClientId, l.MealDate });
            });

            builder.Entity<MealLogItem>(entity =>
            {
                entity.ToTable(nameof(MealLogItem));

                // R25, composition; R26
                entity.HasOne(i => i.MealLog).WithMany(l => l.Items)
                    .HasForeignKey(i => i.MealLogId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(i => i.FoodItem).WithMany(f => f.MealLogItems)
                    .HasForeignKey(i => i.FoodItemId).OnDelete(DeleteBehavior.Restrict);
            });
        }

        private static void ConfigureHealth(ModelBuilder builder, EncryptedStringConverter encrypted)
        {
            builder.Entity<HealthMeasurement>(entity =>
            {
                entity.ToTable(nameof(HealthMeasurement), table =>
                    table.HasCheckConstraint("CK_HealthMeasurement_DiastolicBelowSystolic",
                        "[DiastolicBp] IS NULL OR [SystolicBp] IS NULL OR [DiastolicBp] < [SystolicBp]"));

                // R27
                entity.HasOne(m => m.Client).WithMany(c => c.Measurements)
                    .HasForeignKey(m => m.ClientId).OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(m => new { m.ClientId, m.MeasuredAt });

                // BR-20
                entity.Property(m => m.Notes).HasConversion(encrypted);
            });
        }

        private static void ConfigureAppointments(ModelBuilder builder, EncryptedStringConverter encrypted)
        {
            builder.Entity<Appointment>(entity =>
            {
                entity.ToTable(nameof(Appointment));

                // R28, R29
                entity.HasOne(a => a.Client).WithMany(c => c.Appointments)
                    .HasForeignKey(a => a.ClientId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(a => a.Trainer).WithMany(t => t.Appointments)
                    .HasForeignKey(a => a.TrainerId).OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(a => new { a.TrainerId, a.StartDateTime });
                entity.HasIndex(a => new { a.ClientId, a.StartDateTime });

                // BR-20
                entity.Property(a => a.ClientReason).HasConversion(encrypted);
                entity.Property(a => a.ProfessionalNotes).HasConversion(encrypted);
            });

            builder.Entity<Feedback>(entity =>
            {
                entity.ToTable(nameof(Feedback));

                // R30, R31, R32
                entity.HasOne(f => f.Trainer).WithMany(t => t.Feedback)
                    .HasForeignKey(f => f.TrainerId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(f => f.Client).WithMany(c => c.Feedback)
                    .HasForeignKey(f => f.ClientId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(f => f.WorkoutLog).WithMany(l => l.Feedback)
                    .HasForeignKey(f => f.WorkoutLogId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
            });
        }

        private static void ConfigureNotificationsAndAudit(ModelBuilder builder)
        {
            builder.Entity<Notification>(entity =>
            {
                entity.ToTable(nameof(Notification));

                // R6, composition
                entity.HasOne(n => n.User).WithMany(u => u.Notifications)
                    .HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(n => new { n.UserId, n.IsRead });

                // BR-19: each reminder is created once; ordinary notifications have no key.
                entity.HasIndex(n => n.ReminderKey).IsUnique().HasFilter("[ReminderKey] IS NOT NULL");
            });

            builder.Entity<NotificationPreference>(entity =>
            {
                entity.ToTable(nameof(NotificationPreference));

                // R5, composition; the key is also the foreign key.
                entity.HasOne(p => p.User).WithOne(u => u.NotificationPreference)
                    .HasForeignKey<NotificationPreference>(p => p.UserId).OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<AuditLog>(entity =>
            {
                entity.ToTable(nameof(AuditLog));

                // R7: optional, so failed logins with an unknown email can be recorded.
                entity.HasOne(a => a.User).WithMany(u => u.AuditLogs)
                    .HasForeignKey(a => a.UserId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(a => a.Timestamp);
            });
        }
    }
}
