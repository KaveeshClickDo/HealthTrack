using System.ComponentModel.DataAnnotations;
using HealthTrack.Security;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// The form for recording a measurement (UC11). BMI isn't on it: it is always calculated (BR-14).
    /// </summary>
    public class MeasurementViewModel : IValidatableObject
    {
        /// <summary>When the readings were taken; not in the future (BR-13).</summary>
        [Required(ErrorMessage = "Enter when you measured.")]
        [NotInFuture]
        [DataType(DataType.DateTime)]
        [Display(Name = "Measured at")]
        public DateTime? MeasuredAt { get; set; }

        /// <summary>Weight in kg, 20–350.</summary>
        [Required(ErrorMessage = "Enter your weight.")]
        [Range(20.0, 350.0, ErrorMessage = "Weight must be between 20 and 350 kg.")]
        [Display(Name = "Weight (kg)")]
        public decimal? WeightKg { get; set; }

        /// <summary>Height in cm, 50–250; pre-filled from the last reading.</summary>
        [Required(ErrorMessage = "Enter your height.")]
        [Range(50.0, 250.0, ErrorMessage = "Height must be between 50 and 250 cm.")]
        [Display(Name = "Height (cm)")]
        public decimal? HeightCm { get; set; }

        /// <summary>Body fat in percent, 2–70.</summary>
        [Range(2.0, 70.0, ErrorMessage = "Body fat must be between 2 and 70 %.")]
        [Display(Name = "Body fat (%)")]
        public decimal? BodyFatPercent { get; set; }

        /// <summary>Waist in cm, 30–250.</summary>
        [Range(30.0, 250.0, ErrorMessage = "Waist must be between 30 and 250 cm.")]
        [Display(Name = "Waist (cm)")]
        public decimal? WaistCm { get; set; }

        /// <summary>Resting heart rate, 30–220 bpm.</summary>
        [Range(30, 220, ErrorMessage = "Resting heart rate must be between 30 and 220 bpm.")]
        [Display(Name = "Resting heart rate (bpm)")]
        public int? RestingHeartRate { get; set; }

        /// <summary>Systolic blood pressure, 70–250 mmHg.</summary>
        [Range(70, 250, ErrorMessage = "Systolic pressure must be between 70 and 250 mmHg.")]
        [Display(Name = "Systolic (mmHg)")]
        public int? SystolicBp { get; set; }

        /// <summary>Diastolic blood pressure, 40–150 mmHg; lower than systolic.</summary>
        [Range(40, 150, ErrorMessage = "Diastolic pressure must be between 40 and 150 mmHg.")]
        [Display(Name = "Diastolic (mmHg)")]
        public int? DiastolicBp { get; set; }

        /// <summary>Blood glucose, 20–600 mg/dL.</summary>
        [Range(20.0, 600.0, ErrorMessage = "Blood glucose must be between 20 and 600 mg/dL.")]
        [Display(Name = "Blood glucose (mg/dL)")]
        public decimal? BloodGlucose { get; set; }

        /// <summary>The client's notes; stored encrypted (BR-20).</summary>
        [StringLength(1000)]
        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        /// <summary>
        /// The diastolic pressure must be lower than the systolic one (spec 2.6).
        /// </summary>
        /// <param name="validationContext">The validation context.</param>
        /// <returns>The error, if any.</returns>
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (DiastolicBp is { } diastolic && SystolicBp is { } systolic && diastolic >= systolic)
            {
                yield return new ValidationResult("The diastolic pressure must be lower than the systolic pressure.", [nameof(DiastolicBp)]);
            }
        }
    }
}
