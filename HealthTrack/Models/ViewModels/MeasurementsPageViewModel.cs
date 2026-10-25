namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// The client's measurements page: the latest reading with its BMI category, and the history.
    /// </summary>
    public class MeasurementsPageViewModel
    {
        /// <summary>The most recent reading, or null when there are none.</summary>
        public MeasurementRowViewModel? Latest { get; init; }

        /// <summary>The first reading, for the change since the start.</summary>
        public MeasurementRowViewModel? First { get; init; }

        /// <summary>The client's target weight, if set.</summary>
        public decimal? TargetWeightKg { get; init; }

        /// <summary>One page of readings, newest first.</summary>
        public PagedResult<MeasurementRowViewModel> Results { get; init; } = new([], 1, 20, 0);
    }
}
