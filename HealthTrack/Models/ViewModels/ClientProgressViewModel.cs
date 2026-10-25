namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// The client's progress page (UC12): the range and a summary; the charts load their data as JSON.
    /// </summary>
    public class ClientProgressViewModel
    {
        /// <summary>The chosen range.</summary>
        public ProgressRangeViewModel Range { get; init; } = new();

        /// <summary>The most recent measurement, if any.</summary>
        public MeasurementRowViewModel? Latest { get; init; }

        /// <summary>The first measurement, for the change since the start.</summary>
        public MeasurementRowViewModel? First { get; init; }

        /// <summary>The client's target weight, if set.</summary>
        public decimal? TargetWeightKg { get; init; }
    }
}
