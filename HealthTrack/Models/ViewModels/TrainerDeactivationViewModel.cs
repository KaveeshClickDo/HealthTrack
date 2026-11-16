using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// The administrator's form for closing the account of a professional who has left (EXT-01, BR-23).
    /// </summary>
    public class TrainerDeactivationViewModel
    {
        /// <summary>Why the account is closed; shown to clients whose sessions are cancelled, and kept in the audit log.</summary>
        [Required(ErrorMessage = "Give the reason for closing the account.")]
        [StringLength(300, MinimumLength = 5, ErrorMessage = "The reason must be between 5 and 300 characters.")]
        [Display(Name = "Reason")]
        public string Reason { get; set; } = string.Empty;
    }
}
