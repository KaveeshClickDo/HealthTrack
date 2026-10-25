using System.ComponentModel.DataAnnotations;
using HealthTrack.Models.Enums;

namespace HealthTrack.Models.Entities
{
    /// <summary>
    /// One audited event: who did what, when and from where. Never holds health values or passwords.
    /// </summary>
    public class AuditLog
    {
        /// <summary>Primary key.</summary>
        public long AuditLogId { get; set; }

        /// <summary>The user who acted; null for a failed login with an unknown email (R7).</summary>
        [MaxLength(450)]
        public string? UserId { get; set; }

        /// <summary>What happened.</summary>
        public AuditAction Action { get; set; }

        /// <summary>The kind of record affected, for example Appointment.</summary>
        [MaxLength(50)]
        public string? EntityName { get; set; }

        /// <summary>The key of the affected record.</summary>
        [MaxLength(50)]
        public string? EntityId { get; set; }

        /// <summary>A short summary; never health values or passwords.</summary>
        [MaxLength(500)]
        public string? Details { get; set; }

        /// <summary>The client's IPv4 or IPv6 address.</summary>
        [MaxLength(45)]
        public string? IpAddress { get; set; }

        /// <summary>When it happened.</summary>
        public DateTime Timestamp { get; set; }

        /// <summary>The user who acted (R7).</summary>
        public virtual ApplicationUser? User { get; set; }
    }
}
