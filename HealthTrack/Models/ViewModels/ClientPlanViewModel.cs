using HealthTrack.Models.Enums;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// One of a client's workout or diet plans on the administrator's user page, with whether it can be moved to another
    /// client because it was assigned to the wrong one (EXT-02, BR-24).
    /// </summary>
    public class ClientPlanViewModel
    {
        /// <summary>Whether this is a diet plan; otherwise a workout plan.</summary>
        public bool IsDietPlan { get; init; }

        /// <summary>The plan's ID.</summary>
        public int PlanId { get; init; }

        /// <summary>The plan's title.</summary>
        public string Title { get; init; } = string.Empty;

        /// <summary>The plan's status.</summary>
        public PlanStatus Status { get; init; }

        /// <summary>The professional who wrote it.</summary>
        public string TrainerName { get; init; } = string.Empty;

        /// <summary>When it starts.</summary>
        public DateTime? StartDate { get; init; }

        /// <summary>Whether the client has logged anything against it; a used plan stays where it is.</summary>
        public bool HasLogs { get; init; }

        /// <summary>The other clients the plan could move to: those actively assigned to the plan's professional.</summary>
        public IReadOnlyList<SelectOptionViewModel> CorrectionTargets { get; init; } = [];

        /// <summary>Whether the correction is possible at all: a draft or active plan with no logs, and somewhere to move it.</summary>
        public bool CanCorrect => Status is PlanStatus.Draft or PlanStatus.Active && !HasLogs && CorrectionTargets.Count > 0;
    }
}
