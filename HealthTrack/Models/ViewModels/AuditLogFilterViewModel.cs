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

        /// <summary>
        /// Only this kind of event. Not called Action: "action" is a route value, and route values win over the
        /// query string in model binding, so the filter would never apply.
        /// </summary>
        [Display(Name = "Action")]
        public AuditAction? AuditAction { get; set; }

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
