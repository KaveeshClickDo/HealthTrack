namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// The administrator's dashboard (UC28, spec 1.4): users, approvals, assignments, usage and recent audit events.
    /// </summary>
    public class AdminDashboardViewModel
    {
        /// <summary>The administrator's first name, for the greeting.</summary>
        public string FirstName { get; init; } = string.Empty;

        /// <summary>Accounts by role and status.</summary>
        public IReadOnlyList<RoleStatusCountViewModel> UsersByRole { get; init; } = [];

        /// <summary>All accounts.</summary>
        public int TotalUsers => UsersByRole.Sum(r => r.Total);

        /// <summary>Accounts registered since the first of this month.</summary>
        public int NewRegistrationsThisMonth { get; init; }

        /// <summary>Accounts that signed in during the last 7 days.</summary>
        public int ActiveUsersLast7Days { get; init; }

        /// <summary>Professionals waiting for approval (UC30).</summary>
        public int TrainersAwaitingApproval { get; init; }

        /// <summary>Active clients without an active trainer, oldest registration first.</summary>
        public IReadOnlyList<UnassignedClientViewModel> UnassignedClients { get; init; } = [];

        /// <summary>Each approved professional's active clients against capacity.</summary>
        public IReadOnlyList<TrainerLoadViewModel> TrainerLoad { get; init; } = [];

        /// <summary>Successful logins per day for the last 14 days (line chart).</summary>
        public ChartSeries LoginsPerDay { get; init; } = new();

        /// <summary>All appointments by status (bar chart).</summary>
        public ChartSeries AppointmentsByStatus { get; init; } = new();

        /// <summary>The 10 newest audit events.</summary>
        public IReadOnlyList<AuditLogItemViewModel> RecentAuditEvents { get; init; } = [];
    }
}
