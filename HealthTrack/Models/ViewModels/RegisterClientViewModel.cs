using System.ComponentModel.DataAnnotations;
using HealthTrack.Models.Enums;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// The client registration form (UC01): account details plus the health profile; height and weight become the first measurement.
    /// </summary>
    public class RegisterClientViewModel : RegisterViewModel
    {
        /// <summary>The client's main goal.</summary>
        [Required(ErrorMessage = "Choose your main goal.")]
        [Display(Name = "Fitness goal")]
        public FitnessGoal? FitnessGoal { get; set; }

        /// <summary>How active the client is day to day.</summary>
        [Required(ErrorMessage = "Choose your activity level.")]
        [Display(Name = "Activity level")]
        public ActivityLevel? ActivityLevel { get; set; }

        /// <summary>Height in cm, 50–250.</summary>
        [Required(ErrorMessage = "Enter your height.")]
        [Range(50.0, 250.0, ErrorMessage = "Height must be between 50 and 250 cm.")]
        [Display(Name = "Height (cm)")]
        public decimal? HeightCm { get; set; }

        /// <summary>Weight in kg, 20–350.</summary>
        [Required(ErrorMessage = "Enter your weight.")]
        [Range(20.0, 350.0, ErrorMessage = "Weight must be between 20 and 350 kg.")]
        [Display(Name = "Weight (kg)")]
        public decimal? WeightKg { get; set; }

        /// <summary>Target weight in kg, 30–300; optional.</summary>
        [Range(30.0, 300.0, ErrorMessage = "Target weight must be between 30 and 300 kg.")]
        [Display(Name = "Target weight (kg)")]
        public decimal? TargetWeightKg { get; set; }

        /// <summary>Health conditions; stored encrypted (BR-20).</summary>
        [StringLength(1000)]
        [Display(Name = "Health conditions")]
        public string? MedicalConditions { get; set; }

        /// <summary>Allergies; stored encrypted (BR-20).</summary>
        [StringLength(1000)]
        [Display(Name = "Allergies")]
        public string? Allergies { get; set; }

        /// <summary>Current medications; stored encrypted (BR-20).</summary>
        [StringLength(1000)]
        [Display(Name = "Current medications")]
        public string? CurrentMedications { get; set; }

        /// <summary>Emergency contact's name.</summary>
        [StringLength(100)]
        [RegularExpression(ValidationPatterns.PersonName, ErrorMessage = ValidationPatterns.PersonNameMessage)]
        [Display(Name = "Emergency contact name")]
        public string? EmergencyContactName { get; set; }

        /// <summary>Emergency contact's phone number.</summary>
        [StringLength(20)]
        [RegularExpression(ValidationPatterns.Phone, ErrorMessage = ValidationPatterns.PhoneMessage)]
        [Display(Name = "Emergency contact phone")]
        public string? EmergencyContactPhone { get; set; }
    }
}
