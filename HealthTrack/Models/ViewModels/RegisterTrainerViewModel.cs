using System.ComponentModel.DataAnnotations;
using HealthTrack.Models.Enums;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// The trainer and health professional registration form (UC02). The account starts PendingApproval (BR-03).
    /// </summary>
    public class RegisterTrainerViewModel : RegisterViewModel, IValidatableObject
    {
        /// <summary>The message shown when a licence number is missing (BR-03).</summary>
        public const string LicenseRequiredMessage =
            "Enter your licence number. Nutritionists, physiotherapists and medical doctors need one.";

        /// <summary>The kind of professional.</summary>
        [Required(ErrorMessage = "Choose your profession.")]
        [Display(Name = "Professional type")]
        public ProfessionalType? ProfessionalType { get; set; }

        /// <summary>Area of expertise.</summary>
        [Required(ErrorMessage = "Enter your specialisation.")]
        [StringLength(100)]
        [Display(Name = "Specialisation")]
        public string Specialization { get; set; } = string.Empty;

        /// <summary>Certificates and degrees.</summary>
        [Required(ErrorMessage = "List your qualifications.")]
        [StringLength(500)]
        [Display(Name = "Qualifications")]
        public string Qualifications { get; set; } = string.Empty;

        /// <summary>Licence number; required unless a fitness trainer (BR-03).</summary>
        [StringLength(50)]
        [Display(Name = "Licence number")]
        public string? LicenseNumber { get; set; }

        /// <summary>Years of experience, 0–60.</summary>
        [Required(ErrorMessage = "Enter your years of experience.")]
        [Range(0, 60, ErrorMessage = "Years of experience must be between 0 and 60.")]
        [Display(Name = "Years of experience")]
        public int? YearsOfExperience { get; set; }

        /// <summary>A short biography shown to clients when they book.</summary>
        [StringLength(1000)]
        [Display(Name = "Short bio")]
        public string? Bio { get; set; }

        /// <summary>
        /// BR-03: a licence number is required unless the professional is a fitness trainer.
        /// wwwroot/js/register-trainer.js applies the same rule in the browser.
        /// </summary>
        /// <param name="validationContext">The validation context.</param>
        /// <returns>The licence error, if any.</returns>
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (ProfessionalType is { } type && type != Enums.ProfessionalType.FitnessTrainer
                && string.IsNullOrWhiteSpace(LicenseNumber))
            {
                yield return new ValidationResult(LicenseRequiredMessage, [nameof(LicenseNumber)]);
            }
        }
    }
}
