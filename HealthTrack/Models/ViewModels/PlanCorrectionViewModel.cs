using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// The administrator's form for moving a plan that was assigned to the wrong client (EXT-02, BR-24).
    /// </summary>
    public class PlanCorrectionViewModel
    {
        /// <summary>Whether the plan is a diet plan; otherwise a workout plan.</summary>
        public bool IsDietPlan { get; set; }

        /// <summary>The plan to move.</summary>
        [Required]
        public int? PlanId { get; set; }

        /// <summary>The client the plan should have gone to.</summary>
        [Required(ErrorMessage = "Choose the client the plan should belong to.")]
        [Display(Name = "Correct client")]
        public int? NewClientId { get; set; }

        /// <summary>Why the plan is being moved; kept in the audit log and shown to the professional.</summary>
        [Required(ErrorMessage = "Give the reason for the correction.")]
        [StringLength(300, MinimumLength = 5, ErrorMessage = "The reason must be between 5 and 300 characters.")]
        [Display(Name = "Reason")]
        public string Reason { get; set; } = string.Empty;
    }
}
