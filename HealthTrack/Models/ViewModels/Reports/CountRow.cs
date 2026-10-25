namespace HealthTrack.Models.ViewModels.Reports
{
    /// <summary>
    /// A label with a count and its share of the total, for small breakdown tables (categories, statuses, months).
    /// </summary>
    public class CountRow
    {
        /// <summary>What is counted, for example "Strength" or "Oct 2026".</summary>
        public string Label { get; init; } = string.Empty;

        /// <summary>The count.</summary>
        public int Count { get; init; }

        /// <summary>The count as a percentage of the table's total, 1 dp.</summary>
        public decimal Percent { get; init; }
    }
}
