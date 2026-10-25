using HealthTrack.Models.Enums;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// One measurement as shown in tables and summaries, with its calculated BMI.
    /// </summary>
    public class MeasurementRowViewModel
    {
        /// <summary>The measurement's key.</summary>
        public int MeasurementId { get; init; }

        /// <summary>When the readings were taken.</summary>
        public DateTime MeasuredAt { get; init; }

        /// <summary>Weight in kg.</summary>
        public decimal WeightKg { get; init; }

        /// <summary>Height in cm.</summary>
        public decimal HeightCm { get; init; }

        /// <summary>BMI to 1 decimal place, calculated (BR-14).</summary>
        public decimal Bmi { get; init; }

        /// <summary>The WHO category of the BMI.</summary>
        public BmiCategory BmiCategory { get; init; }

        /// <summary>Body fat in percent.</summary>
        public decimal? BodyFatPercent { get; init; }

        /// <summary>Waist in cm.</summary>
        public decimal? WaistCm { get; init; }

        /// <summary>Resting heart rate.</summary>
        public int? RestingHeartRate { get; init; }

        /// <summary>Systolic blood pressure.</summary>
        public int? SystolicBp { get; init; }

        /// <summary>Diastolic blood pressure.</summary>
        public int? DiastolicBp { get; init; }

        /// <summary>Blood glucose.</summary>
        public decimal? BloodGlucose { get; init; }

        /// <summary>The client's notes; null when the viewer may not see them.</summary>
        public string? Notes { get; init; }
    }
}
