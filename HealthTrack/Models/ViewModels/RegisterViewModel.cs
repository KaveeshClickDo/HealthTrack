using System.ComponentModel.DataAnnotations;
using HealthTrack.Models.Enums;
using HealthTrack.Security;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// The fields both registration forms ask for (spec 1.3): name, email, password, date of birth, gender, phone and consent.
    /// </summary>
    public abstract class RegisterViewModel
    {
        /// <summary>The message shown when the privacy notice isn't accepted (BR-02).</summary>
        public const string ConsentMessage = "Please read and accept the privacy notice to create an account.";

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

        /// <summary>Email address, also the user name (BR-01).</summary>
        [Required(ErrorMessage = "Enter your email address.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        [StringLength(256)]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        /// <summary>Password (BR-01).</summary>
        [Required(ErrorMessage = "Choose a password.")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = ValidationPatterns.PasswordMessage)]
        [RegularExpression(ValidationPatterns.Password, ErrorMessage = ValidationPatterns.PasswordMessage)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        /// <summary>The password again.</summary>
        [Required(ErrorMessage = "Type the password again.")]
        [Compare(nameof(Password), ErrorMessage = "The passwords don't match.")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        public string ConfirmPassword { get; set; } = string.Empty;

        /// <summary>Date of birth; the user must be 16 or older (BR-02).</summary>
        [Required(ErrorMessage = "Enter your date of birth.")]
        [MinimumAge(16)]
        [DataType(DataType.Date)]
        [Display(Name = "Date of birth")]
        public DateTime? DateOfBirth { get; set; }

        /// <summary>Gender.</summary>
        [Required(ErrorMessage = "Choose an option.")]
        [Display(Name = "Gender")]
        public Gender? Gender { get; set; }

        /// <summary>Phone number; optional.</summary>
        [StringLength(20)]
        [RegularExpression(ValidationPatterns.Phone, ErrorMessage = ValidationPatterns.PhoneMessage)]
        [Display(Name = "Phone number")]
        public string? PhoneNumber { get; set; }

        /// <summary>Whether the privacy notice was accepted (BR-02).</summary>
        [Required(ErrorMessage = ConsentMessage)]
        [MustBeTrue(ErrorMessage = ConsentMessage)]
        [Display(Name = "I have read and accept the privacy notice")]
        public bool AcceptPrivacy { get; set; }
    }
}
