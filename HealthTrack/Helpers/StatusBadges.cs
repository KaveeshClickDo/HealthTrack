using HealthTrack.Models.Enums;

namespace HealthTrack.Helpers
{
    /// <summary>
    /// The Bootstrap badge colour for each status, so every page shows a status the same way.
    /// </summary>
    public static class StatusBadges
    {
        /// <summary>
        /// The badge class for an account status: active green, pending amber, suspended red, deactivated grey.
        /// </summary>
        /// <param name="status">The account status.</param>
        /// <returns>A Bootstrap text-bg-* class.</returns>
        public static string For(AccountStatus status) => status switch
        {
            AccountStatus.Active => "text-bg-success",
            AccountStatus.PendingApproval => "text-bg-warning",
            AccountStatus.Suspended => "text-bg-danger",
            _ => "text-bg-secondary"
        };

        /// <summary>
        /// The badge class for an audit event: security failures red, approvals and creations green, the rest neutral.
        /// </summary>
        /// <param name="action">The audited action.</param>
        /// <returns>A Bootstrap text-bg-* class.</returns>
        public static string For(AuditAction action) => action switch
        {
            AuditAction.LoginFailed or AuditAction.AccountLocked or AuditAction.Reject or AuditAction.Delete => "text-bg-danger",
            AuditAction.Register or AuditAction.Create or AuditAction.Approve or AuditAction.Assign => "text-bg-success",
            AuditAction.Cancel or AuditAction.Export => "text-bg-warning",
            _ => "text-bg-light border"
        };
    }
}
