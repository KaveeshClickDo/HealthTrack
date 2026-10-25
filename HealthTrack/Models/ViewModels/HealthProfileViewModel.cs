using System.ComponentModel.DataAnnotations;
using HealthTrack.Models.Enums;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// The client's health profile: goal, activity level, target weight, the encrypted medical details (BR-20), the
    /// emergency contact and whether the trainer may see the medical details.
    /// </summary>
    public class HealthProfileViewModel
    {
        /// <summary>The client's main goal; adjusts the suggested calorie target.</summary>
        [Required(ErrorMessage = "Choose a goal.")]
        [Display(Name = "Fitness goal")]
        public FitnessGoal? FitnessGoal { get; set; }

        /// <summary>Day-to-day activity; sets the TDEE factor.</summary>
        [Required(ErrorMessage = "Choose an activity level.")]
        [Display(Name = "Activity level")]
        public ActivityLevel? ActivityLevel { get; set; }

        /// <summary>Target weight in kg, 30–300.</summary>
        [Range(30.0, 300.0, ErrorMessage = "Target weight must be between 30 and 300 kg.")]
        [Display(Name = "Target weight (kg)")]
        public decimal? TargetWeightKg { get; set; }

        /// <summary>Health conditions; stored encrypted.</summary>
        [StringLength(2000)]
        [Display(Name = "Medical conditions")]
        public string? MedicalConditions { get; set; }

        /// <summary>Allergies; stored encrypted.</summary>
        [StringLength(2000)]
        [Display(Name = "Allergies")]
        public string? Allergies { get; set; }

        /// <summary>Current medications; stored encrypted.</summary>
        [StringLength(2000)]
        [Display(Name = "Current medications")]
        public string? CurrentMedications { get; set; }

        /// <summary>Emergency contact's name.</summary>
        [StringLength(100)]
        [Display(Name = "Emergency contact name")]
        public string? EmergencyContactName { get; set; }

        /// <summary>Emergency contact's phone number.</summary>
        [StringLength(20)]
        [RegularExpression(ValidationPatterns.Phone, ErrorMessage = ValidationPatterns.PhoneMessage)]
        [Display(Name = "Emergency contact phone")]
        public string? EmergencyContactPhone { get; set; }

        /// <summary>When false, the trainer can't see the three medical fields.</summary>
        [Display(Name = "Let my trainer see my medical conditions, allergies and medications")]
        public bool ShareMedicalInfoWithTrainer { get; set; }
    }
}
