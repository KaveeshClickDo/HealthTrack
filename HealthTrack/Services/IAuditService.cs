using HealthTrack.Models.Enums;
using HealthTrack.Models.ViewModels;

namespace HealthTrack.Services
{
    /// <summary>
    /// Writes and reads the audit trail (spec 1.9: accountability). Never pass passwords or health values in the details.
    /// </summary>
    public interface IAuditService
    {
        /// <summary>
        /// Records an event, with the current time and the request's IP address, and saves it straight away.
        /// </summary>
        /// <param name="userId">The acting user; null for a failed login with an unknown email.</param>
        /// <param name="action">What happened.</param>
        /// <param name="entityName">The kind of record affected, for example "Appointment".</param>
        /// <param name="entityId">The key of the affected record.</param>
        /// <param name="details">A short summary, cut to 500 characters.</param>
        /// <returns>A task that completes when the row is saved.</returns>
        Task LogAsync(string? userId, AuditAction action, string? entityName = null, string? entityId = null, string? details = null);

        /// <summary>
        /// One page of the audit log, newest first (UC33).
        /// </summary>
        /// <param name="filter">Optional email, action and date filters.</param>
        /// <param name="page">The 1-based page number; out-of-range values are clamped.</param>
        /// <returns>The page of rows.</returns>
        Task<PagedResult<AuditLogItemViewModel>> GetPageAsync(AuditLogFilterViewModel filter, int page);
    }
}
