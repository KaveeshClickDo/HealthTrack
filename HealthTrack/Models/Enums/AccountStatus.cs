using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models.Enums
{
    /// <summary>
    /// Whether an account may sign in. Only <see cref="Active"/> accounts can (BR-03, BR-05).
    /// </summary>
    public enum AccountStatus
    {
        /// <summary>The account can sign in.</summary>
        [Display(Name = "Active")]
        Active = 1,

        /// <summary>A trainer waiting for an administrator's approval (BR-03).</summary>
        [Display(Name = "Pending approval")]
        PendingApproval = 2,

        /// <summary>Blocked by an administrator; signed out at the next security-stamp check (BR-05).</summary>
        [Display(Name = "Suspended")]
        Suspended = 3,

        /// <summary>Closed for good, for example a rejected trainer or a deleted account.</summary>
        [Display(Name = "Deactivated")]
        Deactivated = 4
    }
}
