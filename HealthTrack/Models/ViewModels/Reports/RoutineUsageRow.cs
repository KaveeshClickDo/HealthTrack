using HealthTrack.Models.Enums;

namespace HealthTrack.Models.ViewModels.Reports
{
    /// <summary>
    /// One routine template in the popular routines report (spec 1.7).
    /// </summary>
    public class RoutineUsageRow
    {
        /// <summary>The template's key.</summary>
        public int TemplateId { get; init; }

        /// <summary>The template's title.</summary>
        public string Title { get; init; } = string.Empty;

        /// <summary>The template's difficulty.</summary>
        public DifficultyLevel Difficulty { get; init; }

        /// <summary>The trainer who wrote it.</summary>
        public string AuthorName { get; init; } = string.Empty;

        /// <summary>Clients following a copy of it at some point in the range.</summary>
        public int ClientsAssigned { get; init; }

        /// <summary>Copies that are active now.</summary>
        public int ActivePlans { get; init; }

        /// <summary>Adherence on the days a copy of it was the plan in effect, in the range; null when nothing was scheduled.</summary>
        public int? AdherencePercent { get; init; }
    }
}
