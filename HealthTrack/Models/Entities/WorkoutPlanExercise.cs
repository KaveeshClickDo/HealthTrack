using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models.Entities
{
    /// <summary>
    /// One prescribed exercise on one weekday of a workout plan (resolves WorkoutPlan many-to-many Exercise).
    /// </summary>
    public class WorkoutPlanExercise
    {
        /// <summary>Primary key.</summary>
        public int WorkoutPlanExerciseId { get; set; }

        /// <summary>The plan the line belongs to (R14).</summary>
        public int WorkoutPlanId { get; set; }

        /// <summary>The prescribed exercise (R15).</summary>
        public int ExerciseId { get; set; }

        /// <summary>The weekday, 0 = Sunday to 6 = Saturday.</summary>
        public DayOfWeek DayOfWeek { get; set; }

        /// <summary>Order within the day.</summary>
        public int SortOrder { get; set; }

        /// <summary>Number of sets, 1–20.</summary>
        public int? Sets { get; set; }

        /// <summary>Repetitions per set, 1–100.</summary>
        public int? Reps { get; set; }

        /// <summary>Duration in minutes, 1–300.</summary>
        public int? DurationMinutes { get; set; }

        /// <summary>Rest between sets in seconds, 0–600.</summary>
        public int? RestSeconds { get; set; }

        /// <summary>The trainer's coaching cue.</summary>
        [MaxLength(250)]
        public string? Notes { get; set; }

        /// <summary>The plan the line belongs to (R14).</summary>
        public virtual WorkoutPlan WorkoutPlan { get; set; } = null!;

        /// <summary>The prescribed exercise (R15).</summary>
        public virtual Exercise Exercise { get; set; } = null!;

        /// <summary>
        /// Whether the line prescribes sets and reps, or a duration (BR-11).
        /// </summary>
        /// <returns>True when Sets and Reps are both positive, or DurationMinutes is positive.</returns>
        public bool IsValidPrescription() => (Sets > 0 && Reps > 0) || DurationMinutes > 0;
    }
}
