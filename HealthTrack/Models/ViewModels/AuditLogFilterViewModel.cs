using System.ComponentModel.DataAnnotations;
using HealthTrack.Models.Enums;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// Filters for the audit log viewer (UC33). Every filter is optional.
    /// </summary>
    public class AuditLogFilterViewModel
    {
        /// <summary>Part of the acting user's email address.</summary>
        [StringLength(256)]
        [Display(Name = "User email")]
        public string? UserEmail { get; set; }

        /// <summary>Only this kind of event.</summary>
        [Display(Name = "Action")]
        public AuditAction? Action { get; set; }

        /// <summary>First day to include.</summary>
        [DataType(DataType.Date)]
        [Display(Name = "From")]
        public DateTime? From { get; set; }

        /// <summary>Last day to include.</summary>
        [DataType(DataType.Date)]
        [Display(Name = "To")]
        public DateTime? To { get; set; }
    }
}
