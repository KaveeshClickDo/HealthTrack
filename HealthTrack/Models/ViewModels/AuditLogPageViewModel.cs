namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// The audit log page (UC33): the filters and one page of events.
    /// </summary>
    public class AuditLogPageViewModel
    {
        /// <summary>The filters in use.</summary>
        public AuditLogFilterViewModel Filter { get; init; } = new();

        /// <summary>The page of events, newest first.</summary>
        public PagedResult<AuditLogItemViewModel> Results { get; init; } = new([], 1, 20, 0);
    }
}
