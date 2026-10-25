using System.ComponentModel.DataAnnotations;
using HealthTrack.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Models.Entities
{
    /// <summary>
    /// A client's goal, activity level and encrypted medical details. The hub for the client's plans, logs, measurements and appointments.
    /// </summary>
    public class ClientProfile
    {
        /// <summary>Primary key.</summary>
        [Key]
        public int ClientId { get; set; }

        /// <summary>The login account; unique, so one profile per user (R3).</summary>
        [MaxLength(450)]
        public string UserId { get; set; } = string.Empty;

        /// <summary>The client's main goal; adjusts the suggested calorie target.</summary>
        public FitnessGoal FitnessGoal { get; set; }

        /// <summary>Day-to-day activity; sets the TDEE factor.</summary>
        public ActivityLevel ActivityLevel { get; set; }

        /// <summary>Target weight in kg, 30–300.</summary>
        [Precision(5, 2)]
        public decimal? TargetWeightKg { get; set; }

        /// <summary>Health conditions; encrypted at rest (BR-20).</summary>
        public string? MedicalConditions { get; set; }

        /// <summary>Allergies; encrypted at rest (BR-20).</summary>
        public string? Allergies { get; set; }

        /// <summary>Current medications; encrypted at rest (BR-20).</summary>
        public string? CurrentMedications { get; set; }

        /// <summary>Emergency contact's name.</summary>
        [MaxLength(100)]
        public string? EmergencyContactName { get; set; }

        /// <summary>Emergency contact's phone number.</summary>
        [MaxLength(20)]
        public string? EmergencyContactPhone { get; set; }

        /// <summary>When false, trainers cannot see the three encrypted medical fields. Default true.</summary>
        public bool ShareMedicalInfoWithTrainer { get; set; } = true;

        /// <summary>When the profile was created.</summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>When the profile was last changed.</summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>The login account (R3).</summary>
        public virtual ApplicationUser User { get; set; } = null!;

        /// <summary>Trainer assignments over time, ended ones included (R9).</summary>
        public virtual ICollection<TrainerAssignment> Assignments { get; set; } = new List<TrainerAssignment>();

        /// <summary>The client's workout plans (R12).</summary>
        public virtual ICollection<WorkoutPlan> WorkoutPlans { get; set; } = new List<WorkoutPlan>();

        /// <summary>Workout sessions the client logged (R16).</summary>
        public virtual ICollection<WorkoutLog> WorkoutLogs { get; set; } = new List<WorkoutLog>();

        /// <summary>The client's diet plans (R21).</summary>
        public virtual ICollection<DietPlan> DietPlans { get; set; } = new List<DietPlan>();

        /// <summary>Meals the client logged (R23).</summary>
        public virtual ICollection<MealLog> MealLogs { get; set; } = new List<MealLog>();

        /// <summary>The client's health measurements (R27).</summary>
        public virtual ICollection<HealthMeasurement> Measurements { get; set; } = new List<HealthMeasurement>();

        /// <summary>Appointments the client booked (R28).</summary>
        public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

        /// <summary>Feedback the client received (R31).</summary>
        public virtual ICollection<Feedback> Feedback { get; set; } = new List<Feedback>();

        /// <summary>
        /// The current trainer assignment from the loaded <see cref="Assignments"/>; a client has at most one (BR-06).
        /// </summary>
        /// <returns>The active assignment, or null when the client is unassigned.</returns>
        public TrainerAssignment? GetActiveAssignment() =>
            Assignments.Where(a => a.IsActive).OrderByDescending(a => a.StartDate).FirstOrDefault();

        /// <summary>
        /// The active workout plan from the loaded <see cref="WorkoutPlans"/>; a client has at most one (BR-09).
        /// </summary>
        /// <returns>The active plan, or null when there is none.</returns>
        public WorkoutPlan? GetActiveWorkoutPlan() =>
            WorkoutPlans.Where(p => p.Status == PlanStatus.Active).OrderByDescending(p => p.StartDate).FirstOrDefault();

        /// <summary>
        /// The active diet plan from the loaded <see cref="DietPlans"/>; a client has at most one (BR-09).
        /// </summary>
        /// <returns>The active plan, or null when there is none.</returns>
        public DietPlan? GetActiveDietPlan() =>
            DietPlans.Where(p => p.Status == PlanStatus.Active).OrderByDescending(p => p.StartDate).FirstOrDefault();

        /// <summary>
        /// The most recent reading from the loaded <see cref="Measurements"/>.
        /// </summary>
        /// <returns>The latest measurement, or null when there are none.</returns>
        public HealthMeasurement? GetLatestMeasurement() =>
            Measurements.OrderByDescending(m => m.MeasuredAt).FirstOrDefault();
    }
}
