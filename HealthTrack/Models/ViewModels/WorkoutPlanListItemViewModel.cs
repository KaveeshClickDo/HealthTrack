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

        /// <summary>The client's ID, for the avatar colour.</summary>
        public int? ClientId { get; init; }

        /// <summary>The plan's difficulty.</summary>
        public DifficultyLevel Difficulty { get; init; }

        /// <summary>The first exercise of the week, for the plan's photo; null when the plan has none yet.</summary>
        public string? FirstExerciseName { get; init; }

        /// <summary>That exercise's category, for the photo when the exercise has none of its own.</summary>
        public ExerciseCategory? FirstExerciseCategory { get; init; }
    }
}
