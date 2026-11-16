using HealthTrack.Models.Enums;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// One exercise of today's workout, as a photo tile on the client's dashboard.
    /// </summary>
    public class TodayExerciseViewModel
    {
        /// <summary>The exercise's name.</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>The exercise's category.</summary>
        public ExerciseCategory Category { get; init; }

        /// <summary>The prescription, for example "3 × 12 · rest 60 s".</summary>
        public string Prescription { get; init; } = string.Empty;
    }
}
