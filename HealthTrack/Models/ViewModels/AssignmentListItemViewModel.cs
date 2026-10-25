using HealthTrack.Models.Enums;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// One trainer–client assignment, active or ended.
    /// </summary>
    public class AssignmentListItemViewModel
    {
        /// <summary>The assignment's key.</summary>
        public int AssignmentId { get; init; }

        /// <summary>The client's name.</summary>
        public string ClientName { get; init; } = string.Empty;

        /// <summary>The client's email address.</summary>
        public string ClientEmail { get; init; } = string.Empty;

        /// <summary>The trainer's name.</summary>
        public string TrainerName { get; init; } = string.Empty;

        /// <summary>The trainer's profession.</summary>
        public ProfessionalType ProfessionalType { get; init; }

        /// <summary>First day.</summary>
        public DateTime StartDate { get; init; }

        /// <summary>Last day; null while active.</summary>
        public DateTime? EndDate { get; init; }

        /// <summary>The administrator's note.</summary>
        public string? Notes { get; init; }

        /// <summary>Whether the assignment is current.</summary>
        public bool IsActive => EndDate is null;
    }
}
