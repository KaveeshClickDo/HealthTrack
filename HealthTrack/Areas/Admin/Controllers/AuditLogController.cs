using HealthTrack.Models.ViewModels;
using HealthTrack.Services;
using Microsoft.AspNetCore.Mvc;

namespace HealthTrack.Areas.Admin.Controllers
{
    /// <summary>
    /// View the audit log (UC33).
    /// </summary>
    public class AuditLogController : AdminAreaController
    {
        private readonly IAuditService _audit;

        /// <summary>
        /// Creates the controller.
        /// </summary>
        /// <param name="audit">The audit service.</param>
        public AuditLogController(IAuditService audit)
        {
            _audit = audit;
        }

        /// <summary>
        /// Audit events, newest first, 20 per page, filtered by user email, action and date range.
        /// </summary>
        /// <param name="filter">The filters from the query string.</param>
        /// <param name="page">The page number.</param>
        /// <returns>The list.</returns>
        [HttpGet]
        public async Task<IActionResult> Index(AuditLogFilterViewModel filter, int page = 1) =>
            View(new AuditLogPageViewModel { Filter = filter, Results = await _audit.GetPageAsync(filter, page) });
    }
}
