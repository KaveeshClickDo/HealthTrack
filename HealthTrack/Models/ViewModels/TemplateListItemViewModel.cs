using HealthTrack.Models.Enums;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// One routine template in the shared template list.
    /// </summary>
    public class TemplateListItemViewModel
    {
        /// <summary>The template's key.</summary>
        public int WorkoutPlanId { get; init; }

        /// <summary>Title.</summary>
        public string Title { get; init; } = string.Empty;

        /// <summary>Goal and guidance.</summary>
        public string? Description { get; init; }

        /// <summary>How demanding it is.</summary>
        public DifficultyLevel Difficulty { get; init; }

        /// <summary>The trainer who wrote it.</summary>
        public string AuthorName { get; init; } = string.Empty;

        /// <summary>Whether the signed-in trainer wrote it (only the author edits it).</summary>
        public bool IsOwn { get; init; }

        /// <summary>Number of lines.</summary>
        public int ExerciseCount { get; init; }

        /// <summary>Number of training days.</summary>
        public int DayCount { get; init; }

        /// <summary>Client plans copied from it.</summary>
        public int TimesAssigned { get; init; }

        /// <summary>The first exercise of the week, for the card's photo.</summary>
        public string? FirstExerciseName { get; init; }

        /// <summary>That exercise's category, for the photo when the exercise has none of its own.</summary>
        public ExerciseCategory? FirstExerciseCategory { get; init; }

        /// <summary>The weekdays the routine trains on.</summary>
        public IReadOnlyList<DayOfWeek> Days { get; init; } = [];
    }
}
