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
    }
}
