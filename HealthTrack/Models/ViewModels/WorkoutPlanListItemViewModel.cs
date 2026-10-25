using HealthTrack.Models.Enums;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// One of a trainer's client plans in their plan list.
    /// </summary>
    public class WorkoutPlanListItemViewModel
    {
        /// <summary>The plan's key.</summary>
        public int WorkoutPlanId { get; init; }

        /// <summary>Title.</summary>
        public string Title { get; init; } = string.Empty;

        /// <summary>The client's name.</summary>
        public string ClientName { get; init; } = string.Empty;

        /// <summary>Where the plan is in its lifecycle.</summary>
        public PlanStatus Status { get; init; }

        /// <summary>First day.</summary>
        public DateTime? StartDate { get; init; }

        /// <summary>Last day, if any.</summary>
        public DateTime? EndDate { get; init; }

        /// <summary>Number of lines.</summary>
        public int ExerciseCount { get; init; }

        /// <summary>The template it was copied from, if any.</summary>
        public string? SourceTemplateTitle { get; init; }
    }
}
