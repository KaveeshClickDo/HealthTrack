using HealthTrack.Models.Enums;

namespace HealthTrack.Helpers
{
    /// <summary>
    /// The badge colour for each status, so a status looks the same on every page. Use with the badge class:
    /// <c>class="badge @StatusBadges.For(status)"</c>. An empty result is the neutral grey badge.
    /// </summary>
    public static class StatusBadges
    {
        /// <summary>The colour for an account status.</summary>
        /// <param name="status">The status.</param>
        /// <returns>The badge colour class.</returns>
        public static string For(AccountStatus status) => status switch
        {
            AccountStatus.Active => "b-green",
            AccountStatus.PendingApproval => "b-amber",
            AccountStatus.Suspended => "b-red",
            _ => string.Empty
        };

        /// <summary>The colour for a plan status.</summary>
        /// <param name="status">The status.</param>
        /// <returns>The badge colour class.</returns>
        public static string For(PlanStatus status) => status switch
        {
            PlanStatus.Active => "b-green",
            PlanStatus.Completed => "b-blue",
            _ => string.Empty
        };

        /// <summary>The colour for an appointment status.</summary>
        /// <param name="status">The status.</param>
        /// <returns>The badge colour class.</returns>
        public static string For(AppointmentStatus status) => status switch
        {
            AppointmentStatus.Pending => "b-amber",
            AppointmentStatus.Approved => "b-green",
            AppointmentStatus.Rescheduled => "b-blue",
            AppointmentStatus.Completed => "b-accent",
            AppointmentStatus.NoShow => "b-orange",
            AppointmentStatus.Rejected or AppointmentStatus.CancelledByClient or AppointmentStatus.CancelledByTrainer => "b-red",
            _ => string.Empty
        };

        /// <summary>The colour for a BMI category.</summary>
        /// <param name="category">The category.</param>
        /// <returns>The badge colour class.</returns>
        public static string For(BmiCategory category) => category switch
        {
            BmiCategory.Normal => "b-green",
            BmiCategory.Obese => "b-red",
            _ => "b-amber"
        };

        /// <summary>The colour for an audit action.</summary>
        /// <param name="action">The action.</param>
        /// <returns>The badge colour class.</returns>
        public static string For(AuditAction action) => action switch
        {
            AuditAction.LoginFailed or AuditAction.AccountLocked or AuditAction.Reject or AuditAction.Delete => "b-red",
            AuditAction.Register or AuditAction.Create or AuditAction.Approve or AuditAction.Assign => "b-green",
            AuditAction.Cancel or AuditAction.Export => "b-amber",
            _ => string.Empty
        };
    }
}
