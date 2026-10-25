using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HealthTrack.Models.Enums;

namespace HealthTrack.Models.Entities
{
    /// <summary>
    /// A fitness trainer's or health professional's details, capacity and approval.
    /// </summary>
    public class TrainerProfile
    {
        /// <summary>Primary key.</summary>
        [Key]
        public int TrainerId { get; set; }

        /// <summary>The login account; unique, so one profile per user (R4).</summary>
        [MaxLength(450)]
        public string UserId { get; set; } = string.Empty;

        /// <summary>The kind of professional.</summary>
        public ProfessionalType ProfessionalType { get; set; }

        /// <summary>Area of expertise, for example strength and conditioning.</summary>
        [MaxLength(100)]
        public string Specialization { get; set; } = string.Empty;

        /// <summary>Certificates and degrees.</summary>
        [MaxLength(500)]
        public string Qualifications { get; set; } = string.Empty;

        /// <summary>Professional licence number; required unless a fitness trainer (BR-03).</summary>
        [MaxLength(50)]
        public string? LicenseNumber { get; set; }

        /// <summary>Years of experience, 0–60.</summary>
        public int YearsOfExperience { get; set; }

        /// <summary>Short biography shown to clients when booking.</summary>
        [MaxLength(1000)]
        public string? Bio { get; set; }

        /// <summary>Most active clients the trainer takes, 1–100. Default 20 (BR-07).</summary>
        public int MaxClients { get; set; } = 20;

        /// <summary>When an administrator approved the account; null until then (BR-03).</summary>
        public DateTime? ApprovedAt { get; set; }

        /// <summary>When the profile was created.</summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>The login account (R4).</summary>
        public virtual ApplicationUser User { get; set; } = null!;

        /// <summary>Weekly windows in which the trainer accepts bookings (R8).</summary>
        public virtual ICollection<TrainerAvailability> Availabilities { get; set; } = new List<TrainerAvailability>();

        /// <summary>Client assignments over time, ended ones included (R10).</summary>
        public virtual ICollection<TrainerAssignment> Assignments { get; set; } = new List<TrainerAssignment>();

        /// <summary>Workout plans and templates the trainer wrote (R11).</summary>
        public virtual ICollection<WorkoutPlan> WorkoutPlans { get; set; } = new List<WorkoutPlan>();

        /// <summary>Diet plans the trainer wrote (R20).</summary>
        public virtual ICollection<DietPlan> DietPlans { get; set; } = new List<DietPlan>();

        /// <summary>Appointments with the trainer (R29).</summary>
        public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

        /// <summary>Feedback the trainer gave (R30).</summary>
        public virtual ICollection<Feedback> Feedback { get; set; } = new List<Feedback>();

        /// <summary>Whether an administrator has approved the account (BR-03).</summary>
        [NotMapped]
        public bool IsApproved => ApprovedAt.HasValue;

        /// <summary>
        /// Counts the active assignments (no EndDate) in the loaded <see cref="Assignments"/>.
        /// </summary>
        /// <returns>The number of clients the trainer currently coaches.</returns>
        public int ActiveClientCount() => Assignments.Count(a => a.IsActive);

        /// <summary>
        /// Whether the trainer can take another client: active clients stay below <see cref="MaxClients"/> (BR-07).
        /// Works on the loaded <see cref="Assignments"/>.
        /// </summary>
        /// <returns>True when there is room for one more client.</returns>
        public bool HasCapacity() => ActiveClientCount() < MaxClients;

        /// <summary>
        /// Whether one of the loaded <see cref="Availabilities"/> covers an appointment (BR-15).
        /// </summary>
        /// <param name="start">The appointment's start.</param>
        /// <param name="minutes">The appointment's length in minutes.</param>
        /// <returns>True when some availability window covers the whole appointment.</returns>
        public bool IsAvailable(DateTime start, int minutes) => Availabilities.Any(a => a.Covers(start, minutes));
    }
}
