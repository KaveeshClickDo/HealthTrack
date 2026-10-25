namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// What Views/Shared/_ProgressCharts.cshtml draws. The client's page gives URLs that wwwroot/js/progress-charts.js
    /// fetches as JSON; the trainer's page embeds the series instead.
    /// </summary>
    public class ProgressChartsViewModel
    {
        /// <summary>Weight with the target weight as a dashed line (embedded).</summary>
        public ChartSeries? Weight { get; init; }

        /// <summary>BMI (embedded).</summary>
        public ChartSeries? Bmi { get; init; }

        /// <summary>Weekly kcal burned and eaten against the diet target (embedded).</summary>
        public ChartSeries? Calories { get; init; }

        /// <summary>Resting heart rate and blood pressure (embedded).</summary>
        public ChartSeries? Vitals { get; init; }

        /// <summary>The JSON action with the weight and BMI series (fetched).</summary>
        public string? WeightDataUrl { get; init; }

        /// <summary>The JSON action with the calories series (fetched).</summary>
        public string? CaloriesDataUrl { get; init; }

        /// <summary>The JSON action with the vitals series (fetched).</summary>
        public string? VitalsDataUrl { get; init; }
    }
}
