using HealthTrack.Helpers;
using HealthTrack.Models.Enums;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// One prescribed exercise as the client (or a trainer browsing a template) sees it.
    /// </summary>
    public class PlanLineViewModel
    {
        /// <summary>The exercise's name.</summary>
        public string ExerciseName { get; init; } = string.Empty;

        /// <summary>The exercise's category.</summary>
        public ExerciseCategory Category { get; init; }

        /// <summary>Equipment needed; null means body weight.</summary>
        public string? Equipment { get; init; }

        /// <summary>Sets.</summary>
        public int? Sets { get; init; }

        /// <summary>Reps per set.</summary>
        public int? Reps { get; init; }

        /// <summary>Duration in minutes.</summary>
        public int? DurationMinutes { get; init; }

        /// <summary>Rest between sets in seconds.</summary>
        public int? RestSeconds { get; init; }

        /// <summary>The trainer's coaching cue.</summary>
        public string? Notes { get; init; }

        /// <summary>The prescription as text, for example "3 × 10 · rest 60 s".</summary>
        public string Prescription => WorkoutText.Prescription(Sets, Reps, DurationMinutes, RestSeconds);
    }
}
