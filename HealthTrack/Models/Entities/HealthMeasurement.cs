using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HealthTrack.Helpers;
using HealthTrack.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Models.Entities
{
    /// <summary>
    /// One dated set of readings. BMI and its category are computed, never typed or stored (BR-14).
    /// </summary>
    public class HealthMeasurement
    {
        /// <summary>Primary key.</summary>
        [Key]
        public int MeasurementId { get; set; }

        /// <summary>The client measured (R27).</summary>
        public int ClientId { get; set; }

        /// <summary>When the readings were taken; not in the future (BR-13).</summary>
        public DateTime MeasuredAt { get; set; }

        /// <summary>Weight in kg, 20–350.</summary>
        [Precision(5, 2)]
        public decimal WeightKg { get; set; }

        /// <summary>Height in cm, 50–250; pre-filled from the last reading.</summary>
        [Precision(5, 1)]
        public decimal HeightCm { get; set; }

        /// <summary>Body fat in percent, 2–70.</summary>
        [Precision(4, 1)]
        public decimal? BodyFatPercent { get; set; }

        /// <summary>Waist in cm, 30–250.</summary>
        [Precision(5, 1)]
        public decimal? WaistCm { get; set; }

        /// <summary>Resting heart rate, 30–220 bpm.</summary>
        public int? RestingHeartRate { get; set; }

        /// <summary>Systolic blood pressure, 70–250 mmHg.</summary>
        public int? SystolicBp { get; set; }

        /// <summary>Diastolic blood pressure, 40–150 mmHg; lower than systolic (check constraint).</summary>
        public int? DiastolicBp { get; set; }

        /// <summary>Blood glucose, 20–600 mg/dL.</summary>
        [Precision(5, 1)]
        public decimal? BloodGlucose { get; set; }

        /// <summary>The client's notes; encrypted at rest (BR-20).</summary>
        public string? Notes { get; set; }

        /// <summary>The client measured (R27).</summary>
        public virtual ClientProfile Client { get; set; } = null!;

        /// <summary>Body mass index to 1 decimal place, from WeightKg and HeightCm.</summary>
        [NotMapped]
        public decimal Bmi => HealthCalculator.CalculateBmi(WeightKg, HeightCm);

        /// <summary>The WHO category of <see cref="Bmi"/>.</summary>
        [NotMapped]
        public BmiCategory BmiCategory => HealthCalculator.GetBmiCategory(Bmi);
    }
}
