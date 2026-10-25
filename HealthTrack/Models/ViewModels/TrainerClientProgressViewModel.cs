using HealthTrack.Models.Enums;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// An assigned client's progress as their trainer sees it (UC18): charts, adherence, latest measurements and recent logs.
    /// The medical fields are filled only when the client shares them.
    /// </summary>
    public class TrainerClientProgressViewModel
    {
        /// <summary>The client profile's key.</summary>
        public int ClientId { get; init; }

        /// <summary>The client's name.</summary>
        public string ClientName { get; init; } = string.Empty;

        /// <summary>The client's goal.</summary>
        public FitnessGoal FitnessGoal { get; init; }

        /// <summary>The client's activity level.</summary>
        public ActivityLevel ActivityLevel { get; init; }

        /// <summary>The client's age.</summary>
        public int Age { get; init; }

        /// <summary>The client's target weight, if set.</summary>
        public decimal? TargetWeightKg { get; init; }

        /// <summary>Whether the client shares their medical details with the trainer.</summary>
        public bool SharesMedicalInfo { get; init; }

        /// <summary>Health conditions; null unless shared.</summary>
        public string? MedicalConditions { get; init; }

        /// <summary>Allergies; null unless shared.</summary>
        public string? Allergies { get; init; }

        /// <summary>Current medications; null unless shared.</summary>
        public string? CurrentMedications { get; init; }

        /// <summary>The chosen range.</summary>
        public ProgressRangeViewModel Range { get; init; } = new();

        /// <summary>The most recent measurement, if any (notes only when shared).</summary>
        public MeasurementRowViewModel? Latest { get; init; }

        /// <summary>The client's recent measurements, newest first.</summary>
        public IReadOnlyList<MeasurementRowViewModel> RecentMeasurements { get; init; } = [];

        /// <summary>Workout adherence over the range.</summary>
        public AdherenceViewModel Adherence { get; init; } = new();

        /// <summary>The charts for the range, embedded in the page.</summary>
        public ProgressChartsViewModel Charts { get; init; } = new();

        /// <summary>The five most recent sessions.</summary>
        public IReadOnlyList<RecentWorkoutViewModel> RecentWorkouts { get; init; } = [];

        /// <summary>The five most recent days with logged meals.</summary>
        public IReadOnlyList<DailyNutritionViewModel> RecentMealDays { get; init; } = [];
    }
}
