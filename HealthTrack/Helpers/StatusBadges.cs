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
        /// The badge class for a plan status: active green, draft grey, completed blue, archived dark.
        /// </summary>
        /// <param name="status">The plan status.</param>
        /// <returns>A Bootstrap text-bg-* class.</returns>
        public static string For(PlanStatus status) => status switch
        {
            PlanStatus.Active => "text-bg-success",
            PlanStatus.Draft => "text-bg-secondary",
            PlanStatus.Completed => "text-bg-primary",
            _ => "text-bg-dark"
        };

        /// <summary>
        /// The badge class for an appointment status, in the lifecycle diagram's colours: Completed green, No-show amber,
        /// Rejected and both cancellations red, the open states blue (confirmed) or grey (pending).
        /// </summary>
        /// <param name="status">The appointment status.</param>
        /// <returns>A Bootstrap text-bg-* class.</returns>
        public static string For(AppointmentStatus status) => status switch
        {
            AppointmentStatus.Completed => "text-bg-success",
            AppointmentStatus.NoShow => "text-bg-warning",
            AppointmentStatus.Rejected or AppointmentStatus.CancelledByClient or AppointmentStatus.CancelledByTrainer => "text-bg-danger",
            AppointmentStatus.Approved or AppointmentStatus.Rescheduled => "text-bg-primary",
            _ => "text-bg-secondary"
        };

        /// <summary>
        /// The badge class for a BMI category: normal green, under- and overweight amber, obese red.
        /// </summary>
        /// <param name="category">The BMI category.</param>
        /// <returns>A Bootstrap text-bg-* class.</returns>
        public static string For(BmiCategory category) => category switch
        {
            BmiCategory.Normal => "text-bg-success",
            BmiCategory.Obese => "text-bg-danger",
            _ => "text-bg-warning"
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
