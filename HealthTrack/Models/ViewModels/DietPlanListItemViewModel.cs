using HealthTrack.Models.Enums;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// One row of the trainer's diet plan list.
    /// </summary>
    public class DietPlanListItemViewModel
    {
        /// <summary>The plan's key.</summary>
        public int DietPlanId { get; init; }

        /// <summary>Title.</summary>
        public string Title { get; init; } = string.Empty;

        /// <summary>The client's name.</summary>
        public string ClientName { get; init; } = string.Empty;

        /// <summary>Where the plan is in its lifecycle.</summary>
        public PlanStatus Status { get; init; }

        /// <summary>Daily calorie target.</summary>
        public int DailyCalorieTarget { get; init; }

        /// <summary>First day.</summary>
        public DateTime StartDate { get; init; }

        /// <summary>Last day, if any.</summary>
        public DateTime? EndDate { get; init; }

        /// <summary>Number of scheduled meals.</summary>
        public int MealCount { get; init; }

        /// <summary>The client's ID, for the avatar colour.</summary>
        public int ClientId { get; init; }

        /// <summary>Daily protein target in grams, if set.</summary>
        public int? ProteinTargetG { get; init; }

        /// <summary>Daily carbohydrate target in grams, if set.</summary>
        public int? CarbsTargetG { get; init; }

        /// <summary>Daily fat target in grams, if set.</summary>
        public int? FatTargetG { get; init; }
    }
}
