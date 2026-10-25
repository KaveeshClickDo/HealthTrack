using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// The login form (UC03).
    /// </summary>
    public class LoginViewModel
    {
        /// <summary>Email address.</summary>
        [Required(ErrorMessage = "Enter your email address.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        /// <summary>Password.</summary>
        [Required(ErrorMessage = "Enter your password.")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        /// <summary>Where to go after signing in; followed only when it is a local URL.</summary>
        public string? ReturnUrl { get; set; }
    }
}
