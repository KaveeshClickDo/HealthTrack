namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// One line or set of bars in a chart; null values leave a gap.
    /// </summary>
    public class ChartDataset
    {
        /// <summary>The legend label, for example "Weight (kg)".</summary>
        public string Label { get; init; } = string.Empty;

        /// <summary>One value per label of the series; null where there is no value.</summary>
        public IReadOnlyList<decimal?> Data { get; init; } = [];

        /// <summary>"line" or "bar".</summary>
        public string Type { get; init; } = "line";

        /// <summary>Draw as a dashed reference line, for example the target weight.</summary>
        public bool Dashed { get; init; }
    }
}
