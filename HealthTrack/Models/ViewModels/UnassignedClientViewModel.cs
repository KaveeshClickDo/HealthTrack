namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// A client without an active trainer, on the admin dashboard.
    /// </summary>
    public class UnassignedClientViewModel
    {
        /// <summary>The client profile's key.</summary>
        public int ClientId { get; init; }

        /// <summary>The client's name.</summary>
        public string FullName { get; init; } = string.Empty;

        /// <summary>The client's email address.</summary>
        public string Email { get; init; } = string.Empty;

        /// <summary>When the client registered.</summary>
        public DateTime JoinedAt { get; init; }
    }
}
