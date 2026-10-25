using System.ComponentModel.DataAnnotations;
using HealthTrack.Models.Enums;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// Filters for the user list (UC29). Every filter is optional.
    /// </summary>
    public class UserFilterViewModel
    {
        /// <summary>Part of the name or email address.</summary>
        [StringLength(256)]
        [Display(Name = "Name or email")]
        public string? Search { get; set; }

        /// <summary>Only users in this role: Admin, Trainer or Client.</summary>
        [Display(Name = "Role")]
        public string? Role { get; set; }

        /// <summary>Only users with this account status.</summary>
        [Display(Name = "Status")]
        public AccountStatus? Status { get; set; }
    }
}
