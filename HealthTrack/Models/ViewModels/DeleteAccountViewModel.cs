using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// The account deletion form (UC16): the client confirms with their password.
    /// </summary>
    public class DeleteAccountViewModel
    {
        /// <summary>The current password.</summary>
        [Required(ErrorMessage = "Enter your password to confirm.")]
        [DataType(DataType.Password)]
        [Display(Name = "Your password")]
        public string? Password { get; set; }
    }
}
