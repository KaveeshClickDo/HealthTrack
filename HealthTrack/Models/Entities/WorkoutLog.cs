using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HealthTrack.Helpers;

namespace HealthTrack.Models.Entities
{
    /// <summary>
    /// A workout session a client performed. Session totals are computed from the entries, not stored.
    /// </summary>
    public class WorkoutLog
    {
        /// <summary>Primary key.</summary>
        public int WorkoutLogId { get; set; }

        /// <summary>The client who trained (R16).</summary>
        public int ClientId { get; set; }

        /// <summary>The plan the session followed; null for a free workout (R17).</summary>
        public int? WorkoutPlanId { get; set; }

        /// <summary>The day of the session; not in the future (BR-13).</summary>
        [Column(TypeName = "date")]
        public DateTime WorkoutDate { get; set; }

        /// <summary>Rating of perceived exertion, 1–10.</summary>
        public int? PerceivedEffort { get; set; }

        /// <summary>The client's notes.</summary>
        [MaxLength(500)]
        public string? Notes { get; set; }

        /// <summary>When the log was saved.</summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>The client who trained (R16).</summary>
        public virtual ClientProfile Client { get; set; } = null!;

        /// <summary>The plan the session followed (R17).</summary>
        public virtual WorkoutPlan? WorkoutPlan { get; set; }

        /// <summary>The exercises done; a saved log has at least one (R18, BR-13).</summary>
        public virtual ICollection<WorkoutLogEntry> Entries { get; set; } = new List<WorkoutLogEntry>();

        /// <summary>Trainer feedback on this session (R32).</summary>
        public virtual ICollection<Feedback> Feedback { get; set; } = new List<Feedback>();

        /// <summary>Total minutes of the loaded entries.</summary>
        [NotMapped]
        public int TotalDurationMinutes => Entries.Sum(e => e.DurationMinutes);

        /// <summary>Total kcal of the loaded entries.</summary>
        [NotMapped]
        public decimal TotalCaloriesBurned => Entries.Sum(e => e.CaloriesBurned);

        /// <summary>
        /// Adds an exercise to the session and stores its calories as a snapshot: MET × weight × minutes / 60 (BR-14),
        /// so later changes to the library or the client's weight don't rewrite history.
        /// </summary>
        /// <param name="exercise">The exercise done.</param>
        /// <param name="weightKg">The client's body weight in kilograms, normally the latest measurement.</param>
        /// <param name="minutes">How long the exercise lasted, 1–300.</param>
        /// <param name="sets">Sets completed, if counted.</param>
        /// <param name="reps">Reps per set, if counted.</param>
        /// <param name="loadKg">Load lifted in kilograms, if any.</param>
        /// <param name="distanceKm">Distance covered in kilometres, for cardio.</param>
        /// <returns>The new entry, already added to <see cref="Entries"/>.</returns>
        public WorkoutLogEntry AddEntry(Exercise exercise, decimal weightKg, int minutes,
            int? sets = null, int? reps = null, decimal? loadKg = null, decimal? distanceKm = null)
        {
            ArgumentNullException.ThrowIfNull(exercise);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minutes);

            // Only the foreign key is set: attaching a library entity loaded without tracking would make EF Core insert it again.
            var entry = new WorkoutLogEntry
            {
                ExerciseId = exercise.ExerciseId,
                SetsCompleted = sets,
                RepsCompleted = reps,
                WeightKg = loadKg,
                DurationMinutes = minutes,
                DistanceKm = distanceKm,
                CaloriesBurned = exercise.EstimateCalories(weightKg, minutes)
            };

            Entries.Add(entry);
            return entry;
        }
    }
}
