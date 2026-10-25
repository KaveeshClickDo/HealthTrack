namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// One chart card (Views/Shared/_ChartCard): a title, the series embedded for wwwroot/js/charts.js, and what to say
    /// when there is nothing to draw.
    /// </summary>
    public class ChartCardViewModel
    {
        /// <summary>The card's heading.</summary>
        public string Title { get; init; } = string.Empty;

        /// <summary>The data.</summary>
        public ChartSeries Series { get; init; } = new();

        /// <summary>Decimal places in tooltips and the data table.</summary>
        public int Decimals { get; init; }

        /// <summary>The data table's first column header, for example "Date" or "Client".</summary>
        public string LabelHeader { get; init; } = "Date";

        /// <summary>Shown instead of the chart when there is no data.</summary>
        public string EmptyText { get; init; } = "Nothing to show for this period.";
    }
}
