using System.ComponentModel.DataAnnotations;
using HealthTrack.Helpers;
using HealthTrack.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Models.Entities
{
    /// <summary>
    /// An exercise library entry with its MET value. Inactive exercises are hidden, never deleted (BR-21).
    /// </summary>
    public class Exercise
    {
        /// <summary>Primary key.</summary>
        public int ExerciseId { get; set; }

        /// <summary>Unique name.</summary>
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        /// <summary>The type of training.</summary>
        public ExerciseCategory Category { get; set; }

        /// <summary>The main muscle group worked.</summary>
        public MuscleGroup MuscleGroup { get; set; }

        /// <summary>Equipment needed; empty means body weight only.</summary>
        [MaxLength(100)]
        public string? Equipment { get; set; }

        /// <summary>Short instructions.</summary>
        [MaxLength(1000)]
        public string? Instructions { get; set; }

        /// <summary>Metabolic equivalent, 1.0–20.0, from the Compendium of Physical Activities; drives calories burned.</summary>
        [Precision(4, 1)]
        public decimal MetValue { get; set; }

        /// <summary>Whether the exercise can be picked for new plans and logs. Default true.</summary>
        public bool IsActive { get; set; } = true;

        /// <summary>Plan lines that prescribe this exercise (R15).</summary>
        public virtual ICollection<WorkoutPlanExercise> WorkoutPlanExercises { get; set; } = new List<WorkoutPlanExercise>();

        /// <summary>Logged entries of this exercise (R19).</summary>
        public virtual ICollection<WorkoutLogEntry> WorkoutLogEntries { get; set; } = new List<WorkoutLogEntry>();

        /// <summary>
        /// Calories this exercise burns: MET × weight × minutes / 60 (spec 1.10, BR-14).
        /// </summary>
        /// <param name="weightKg">The client's body weight in kilograms.</param>
        /// <param name="minutes">How long the exercise lasts.</param>
        /// <returns>kcal burned to 2 decimal places.</returns>
        public decimal EstimateCalories(decimal weightKg, int minutes) =>
            HealthCalculator.CaloriesBurned(MetValue, weightKg, minutes);
    }
}
