using System.ComponentModel.DataAnnotations;
using HealthTrack.Models.Enums;

namespace HealthTrack.Models.Entities
{
    /// <summary>
    /// A trainer's comment to a client, optionally about one workout session.
    /// </summary>
    public class Feedback
    {
        /// <summary>Primary key.</summary>
        public int FeedbackId { get; set; }

        /// <summary>The trainer giving the feedback (R30).</summary>
        public int TrainerId { get; set; }

        /// <summary>The client receiving it (R31).</summary>
        public int ClientId { get; set; }

        /// <summary>The session the feedback is about, if any (R32).</summary>
        public int? WorkoutLogId { get; set; }

        /// <summary>What the feedback is about.</summary>
        public FeedbackCategory Category { get; set; }

        /// <summary>The comment.</summary>
        [MaxLength(1000)]
        public string Message { get; set; } = string.Empty;

        /// <summary>Progress rating, 1–5.</summary>
        public int? Rating { get; set; }

        /// <summary>When the feedback was given.</summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>The trainer giving the feedback (R30).</summary>
        public virtual TrainerProfile Trainer { get; set; } = null!;

        /// <summary>The client receiving it (R31).</summary>
        public virtual ClientProfile Client { get; set; } = null!;

        /// <summary>The session the feedback is about (R32).</summary>
        public virtual WorkoutLog? WorkoutLog { get; set; }
    }
}
