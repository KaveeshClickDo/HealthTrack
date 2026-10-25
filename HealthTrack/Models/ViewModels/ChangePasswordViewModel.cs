using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// The change-password form (UC05); the current password is required.
    /// </summary>
    public class ChangePasswordViewModel
    {
        /// <summary>The password in use now.</summary>
        [Required(ErrorMessage = "Enter your current password.")]
        [DataType(DataType.Password)]
        [Display(Name = "Current password")]
        public string CurrentPassword { get; set; } = string.Empty;

        /// <summary>The new password (BR-01).</summary>
        [Required(ErrorMessage = "Choose a new password.")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = ValidationPatterns.PasswordMessage)]
        [RegularExpression(ValidationPatterns.Password, ErrorMessage = ValidationPatterns.PasswordMessage)]
        [DataType(DataType.Password)]
        [Display(Name = "New password")]
        public string NewPassword { get; set; } = string.Empty;

        /// <summary>The new password again.</summary>
        [Required(ErrorMessage = "Type the new password again.")]
        [Compare(nameof(NewPassword), ErrorMessage = "The passwords don't match.")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm new password")]
        public string ConfirmNewPassword { get; set; } = string.Empty;
    }
}
