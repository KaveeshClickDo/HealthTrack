namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// One row of the admin dashboard's users table: a role's accounts by status.
    /// </summary>
    public class RoleStatusCountViewModel
    {
        /// <summary>The role name.</summary>
        public string Role { get; init; } = string.Empty;

        /// <summary>Active accounts.</summary>
        public int Active { get; init; }

        /// <summary>Accounts waiting for approval.</summary>
        public int PendingApproval { get; init; }

        /// <summary>Suspended accounts.</summary>
        public int Suspended { get; init; }

        /// <summary>Deactivated (deleted) accounts.</summary>
        public int Deactivated { get; init; }

        /// <summary>All the role's accounts.</summary>
        public int Total => Active + PendingApproval + Suspended + Deactivated;
    }
}
