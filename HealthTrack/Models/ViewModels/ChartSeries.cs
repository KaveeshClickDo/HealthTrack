namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// The data for one Chart.js chart: the x-axis labels and one or more datasets (spec 1.7). Serialised as JSON for
    /// wwwroot/js/progress-charts.js.
    /// </summary>
    public class ChartSeries
    {
        /// <summary>The x-axis labels, for example dates.</summary>
        public IReadOnlyList<string> Labels { get; init; } = [];

        /// <summary>The datasets; each has one value per label.</summary>
        public IReadOnlyList<ChartDataset> Datasets { get; init; } = [];

        /// <summary>Whether there is nothing to draw in the range.</summary>
        public bool IsEmpty => Datasets.All(d => d.Data.All(v => v is null));
    }
}
