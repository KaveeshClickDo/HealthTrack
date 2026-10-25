using HealthTrack.Models.Enums;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// The client's active workout plan as a Monday–Sunday week (UC08).
    /// </summary>
    public class WorkoutWeekViewModel
    {
        /// <summary>Whether the client has an active plan; when false, the page explains what to do instead.</summary>
        public bool HasPlan { get; init; }

        /// <summary>Title.</summary>
        public string Title { get; init; } = string.Empty;

        /// <summary>Goal and guidance.</summary>
        public string? Description { get; init; }

        /// <summary>How demanding the plan is.</summary>
        public DifficultyLevel Difficulty { get; init; }

        /// <summary>The trainer who wrote the plan.</summary>
        public string TrainerName { get; init; } = string.Empty;

        /// <summary>First day.</summary>
        public DateTime? StartDate { get; init; }

        /// <summary>Last day, if any.</summary>
        public DateTime? EndDate { get; init; }

        /// <summary>Monday to Sunday.</summary>
        public IReadOnlyList<PlanDayViewModel> Days { get; init; } = [];
    }
}
