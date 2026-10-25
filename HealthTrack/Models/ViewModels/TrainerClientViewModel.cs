using HealthTrack.Models.Enums;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// One of the trainer's assigned clients, for the trainer's client list.
    /// </summary>
    public class TrainerClientViewModel
    {
        /// <summary>The client profile's key.</summary>
        public int ClientId { get; init; }

        /// <summary>The client's name.</summary>
        public string FullName { get; init; } = string.Empty;

        /// <summary>The client's email address.</summary>
        public string Email { get; init; } = string.Empty;

        /// <summary>The client's goal.</summary>
        public FitnessGoal FitnessGoal { get; init; }

        /// <summary>The client's activity level.</summary>
        public ActivityLevel ActivityLevel { get; init; }

        /// <summary>When the assignment started.</summary>
        public DateTime AssignedSince { get; init; }

        /// <summary>The most recent workout date, if any.</summary>
        public DateTime? LastWorkoutDate { get; init; }

        /// <summary>The active workout plan's title, if any.</summary>
        public string? ActivePlanTitle { get; init; }

        /// <summary>The last day the client logged a workout or meal or recorded a measurement, if ever.</summary>
        public DateTime? LastActiveDay { get; init; }

        /// <summary>Workout adherence over the last 7 days, today included (spec 1.10); null when nothing was scheduled.</summary>
        public int? AdherencePercent { get; init; }

        /// <summary>Weight change from the first measurement to the latest, in kg; null with fewer than two.</summary>
        public decimal? WeightChangeKg { get; init; }

        /// <summary>Days since the last activity (or since the assignment started, for a client who never logged anything).</summary>
        public int DaysInactive { get; init; }

        /// <summary>Whether the client has done nothing for 7 days or more (spec 1.4).</summary>
        public bool IsInactive { get; init; }
    }
}
