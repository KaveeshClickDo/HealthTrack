using HealthTrack.Models.Enums;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// One piece of trainer feedback as the client reads it (UC15).
    /// </summary>
    public class FeedbackItemViewModel
    {
        /// <summary>The feedback's key.</summary>
        public int FeedbackId { get; init; }

        /// <summary>The trainer's name.</summary>
        public string TrainerName { get; init; } = string.Empty;

        /// <summary>What the feedback is about.</summary>
        public FeedbackCategory Category { get; init; }

        /// <summary>The comment.</summary>
        public string Message { get; init; } = string.Empty;

        /// <summary>Progress rating, 1–5.</summary>
        public int? Rating { get; init; }

        /// <summary>When it was given.</summary>
        public DateTime CreatedAt { get; init; }

        /// <summary>The date of the session it is about, if any.</summary>
        public DateTime? WorkoutDate { get; init; }
    }
}
