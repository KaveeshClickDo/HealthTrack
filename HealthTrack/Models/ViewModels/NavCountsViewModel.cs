namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// The counts shown next to sidebar links.
    /// </summary>
    public class NavCountsViewModel
    {
        /// <summary>A trainer's actively assigned clients.</summary>
        public int ActiveClients { get; set; }

        /// <summary>A trainer's upcoming appointment requests waiting for an answer.</summary>
        public int PendingRequests { get; set; }

        /// <summary>Professional registrations waiting for an administrator.</summary>
        public int PendingApprovals { get; set; }
    }
}
