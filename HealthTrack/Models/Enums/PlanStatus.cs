using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models.Enums
{
    /// <summary>
    /// The lifecycle of a workout or diet plan: Draft → Active → Completed, or Archived (spec 1.5).
    /// </summary>
    public enum PlanStatus
    {
        /// <summary>Being written by the trainer; not yet visible as the client's plan.</summary>
        [Display(Name = "Draft")]
        Draft = 1,

        /// <summary>The client's current plan; at most one per client and plan kind (BR-09).</summary>
        [Display(Name = "Active")]
        Active = 2,

        /// <summary>Finished, usually because a newer plan was activated (BR-09).</summary>
        [Display(Name = "Completed")]
        Completed = 3,

        /// <summary>Retired instead of deleted (BR-21).</summary>
        [Display(Name = "Archived")]
        Archived = 4
    }
}
