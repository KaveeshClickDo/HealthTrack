using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HealthTrack.Helpers;

namespace HealthTrack.Models.Entities
{
    /// <summary>
    /// Which trainer coaches which client, with history: ended assignments keep their rows.
    /// </summary>
    public class TrainerAssignment
    {
        /// <summary>Primary key.</summary>
        [Key]
        public int AssignmentId { get; set; }

        /// <summary>The client (R9).</summary>
        public int ClientId { get; set; }

        /// <summary>The trainer (R10).</summary>
        public int TrainerId { get; set; }

        /// <summary>First day of the assignment.</summary>
        [Column(TypeName = "date")]
        public DateTime StartDate { get; set; }

        /// <summary>Last day of the assignment; null while it is active. On or after <see cref="StartDate"/>.</summary>
        [Column(TypeName = "date")]
        public DateTime? EndDate { get; set; }

        /// <summary>The administrator's note on the match.</summary>
        [MaxLength(500)]
        public string? Notes { get; set; }

        /// <summary>The client (R9).</summary>
        public virtual ClientProfile Client { get; set; } = null!;

        /// <summary>The trainer (R10).</summary>
        public virtual TrainerProfile Trainer { get; set; } = null!;

        /// <summary>Whether the assignment is current (no EndDate); a client has at most one (BR-06).</summary>
        [NotMapped]
        public bool IsActive => EndDate == null;

        /// <summary>
        /// Ends the assignment, for example when the client gets a new trainer (BR-06).
        /// </summary>
        /// <param name="endDate">The last day of the assignment (the time part is ignored).</param>
        /// <exception cref="BusinessRuleException">The assignment has already ended, or the date is before the start.</exception>
        public void End(DateTime endDate)
        {
            if (!IsActive)
            {
                throw new BusinessRuleException("BR-06", "This assignment has already ended.");
            }

            if (endDate.Date < StartDate)
            {
                throw new BusinessRuleException("BR-06", "An assignment cannot end before it starts.");
            }

            EndDate = endDate.Date;
        }
    }
}
