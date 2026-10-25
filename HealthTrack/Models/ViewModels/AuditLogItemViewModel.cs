using HealthTrack.Models.Enums;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// One row of the audit log viewer.
    /// </summary>
    public class AuditLogItemViewModel
    {
        /// <summary>The row's key.</summary>
        public long AuditLogId { get; init; }

        /// <summary>When it happened.</summary>
        public DateTime Timestamp { get; init; }

        /// <summary>The acting user's email; null for a failed login with an unknown email.</summary>
        public string? UserEmail { get; init; }

        /// <summary>The acting user's name.</summary>
        public string? UserName { get; init; }

        /// <summary>What happened.</summary>
        public AuditAction Action { get; init; }

        /// <summary>The kind of record affected.</summary>
        public string? EntityName { get; init; }

        /// <summary>The key of the affected record.</summary>
        public string? EntityId { get; init; }

        /// <summary>A short summary.</summary>
        public string? Details { get; init; }

        /// <summary>Where the request came from.</summary>
        public string? IpAddress { get; init; }
    }
}
