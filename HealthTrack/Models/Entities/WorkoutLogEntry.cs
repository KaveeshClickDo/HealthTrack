using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Models.Entities
{
    /// <summary>
    /// One exercise done in a session (resolves WorkoutLog many-to-many Exercise). Created by <see cref="WorkoutLog.AddEntry"/>.
    /// </summary>
    public class WorkoutLogEntry
    {
        /// <summary>Primary key.</summary>
        public int WorkoutLogEntryId { get; set; }

        /// <summary>The session (R18).</summary>
        public int WorkoutLogId { get; set; }

        /// <summary>The exercise done (R19).</summary>
        public int ExerciseId { get; set; }

        /// <summary>Sets completed.</summary>
        public int? SetsCompleted { get; set; }

        /// <summary>Reps per set.</summary>
        public int? RepsCompleted { get; set; }

        /// <summary>Load lifted in kilograms.</summary>
        [Precision(6, 2)]
        public decimal? WeightKg { get; set; }

        /// <summary>Duration in minutes, 1–300.</summary>
        public int DurationMinutes { get; set; }

        /// <summary>Distance in kilometres, for cardio.</summary>
        [Precision(6, 2)]
        public decimal? DistanceKm { get; set; }

        /// <summary>kcal burned (MET × body weight × hours), stored as a snapshot.</summary>
        [Precision(7, 2)]
        public decimal CaloriesBurned { get; set; }

        /// <summary>The session (R18).</summary>
        public virtual WorkoutLog WorkoutLog { get; set; } = null!;

        /// <summary>The exercise done (R19).</summary>
        public virtual Exercise Exercise { get; set; } = null!;
    }
}
