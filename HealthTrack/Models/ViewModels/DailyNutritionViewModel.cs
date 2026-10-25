namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// One day's eating against the diet plan's targets (spec 1.10: daily intake is the sum of the day's items).
    /// </summary>
    public class DailyNutritionViewModel
    {
        /// <summary>The day.</summary>
        public DateTime Date { get; init; }

        /// <summary>kcal eaten.</summary>
        public decimal CaloriesEaten { get; init; }

        /// <summary>Protein eaten in grams.</summary>
        public decimal ProteinG { get; init; }

        /// <summary>Carbohydrate eaten in grams.</summary>
        public decimal CarbsG { get; init; }

        /// <summary>Fat eaten in grams.</summary>
        public decimal FatG { get; init; }

        /// <summary>The diet plan's daily kcal target that day, if a plan applied.</summary>
        public int? CalorieTarget { get; init; }

        /// <summary>The protein target in grams, if set.</summary>
        public int? ProteinTargetG { get; init; }

        /// <summary>The carbohydrate target in grams, if set.</summary>
        public int? CarbsTargetG { get; init; }

        /// <summary>The fat target in grams, if set.</summary>
        public int? FatTargetG { get; init; }

        /// <summary>The meals logged that day.</summary>
        public IReadOnlyList<LoggedMealViewModel> Meals { get; init; } = [];

        /// <summary>kcal eaten as a percentage of the target, 0–100+; null without a target.</summary>
        public int? PercentOfTarget => CalorieTarget is > 0
            ? (int)Math.Round(CaloriesEaten * 100m / CalorieTarget.Value, MidpointRounding.AwayFromZero)
            : null;
    }
}
