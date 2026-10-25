using System.ComponentModel.DataAnnotations;
using HealthTrack.Models.Enums;
using HealthTrack.Security;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// The personal details a user can edit (UC05). The email address is the user name and isn't changed here.
    /// </summary>
    public class ProfileViewModel
    {
        /// <summary>First name.</summary>
        [Required(ErrorMessage = "Enter your first name.")]
        [StringLength(50)]
        [RegularExpression(ValidationPatterns.PersonName, ErrorMessage = ValidationPatterns.PersonNameMessage)]
        [Display(Name = "First name")]
        public string FirstName { get; set; } = string.Empty;

        /// <summary>Last name.</summary>
        [Required(ErrorMessage = "Enter your last name.")]
        [StringLength(50)]
        [RegularExpression(ValidationPatterns.PersonName, ErrorMessage = ValidationPatterns.PersonNameMessage)]
        [Display(Name = "Last name")]
        public string LastName { get; set; } = string.Empty;

        /// <summary>Phone number; optional.</summary>
        [StringLength(20)]
        [RegularExpression(ValidationPatterns.Phone, ErrorMessage = ValidationPatterns.PhoneMessage)]
        [Display(Name = "Phone number")]
        public string? PhoneNumber { get; set; }

        /// <summary>Date of birth; 16 or older (BR-02).</summary>
        [Required(ErrorMessage = "Enter your date of birth.")]
        [MinimumAge(16)]
        [DataType(DataType.Date)]
        [Display(Name = "Date of birth")]
        public DateTime? DateOfBirth { get; set; }

        /// <summary>Gender.</summary>
        [Required(ErrorMessage = "Choose an option.")]
        [Display(Name = "Gender")]
        public Gender? Gender { get; set; }
    }
}
