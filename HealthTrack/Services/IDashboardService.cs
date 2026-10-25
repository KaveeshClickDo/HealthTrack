using HealthTrack.Models.ViewModels;

namespace HealthTrack.Services
{
    /// <summary>
    /// One view model per role's dashboard (spec 1.4), built with read-only queries.
    /// </summary>
    public interface IDashboardService
    {
        /// <summary>
        /// The client's dashboard (UC07): latest weight, BMI and change, kcal burned this week, today's workout and meals,
        /// the next 3 appointments, the latest feedback and unread notifications.
        /// </summary>
        /// <param name="clientUserId">The signed-in client.</param>
        /// <returns>The dashboard, or null when the user has no client profile.</returns>
        Task<ClientDashboardViewModel?> GetClientDashboardAsync(string clientUserId);

        /// <summary>
        /// The trainer's dashboard (UC17): assigned clients with last activity, 7-day adherence and weight change (inactive
        /// ones flagged), pending appointment requests and today's schedule.
        /// </summary>
        /// <param name="trainerUserId">The signed-in professional.</param>
        /// <returns>The dashboard, or null when the user has no trainer profile.</returns>
        Task<TrainerDashboardViewModel?> GetTrainerDashboardAsync(string trainerUserId);

        /// <summary>
        /// The administrator's dashboard (UC28): users by role and status, new registrations this month, active users,
        /// approvals, unassigned clients, trainer workload, logins per day, appointments by status and recent audit events.
        /// </summary>
        /// <param name="adminUserId">The signed-in administrator, for the greeting.</param>
        /// <returns>The dashboard.</returns>
        Task<AdminDashboardViewModel> GetAdminDashboardAsync(string adminUserId);
    }
}
