using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// The reason an administrator gives when rejecting a trainer registration (UC30).
    /// </summary>
    public class RejectTrainerViewModel
    {
        /// <summary>Why the registration is rejected; kept in the audit log.</summary>
        [Required(ErrorMessage = "Give a reason for the rejection.")]
        [StringLength(400)]
        [Display(Name = "Reason")]
        public string Reason { get; set; } = string.Empty;
    }
}
