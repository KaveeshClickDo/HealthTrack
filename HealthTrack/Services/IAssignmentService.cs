using HealthTrack.Models.Entities;
using HealthTrack.Models.ViewModels;

namespace HealthTrack.Services
{
    /// <summary>
    /// Trainer–client assignments and the access checks built on them (BR-06 to BR-08).
    /// </summary>
    public interface IAssignmentService
    {
        /// <summary>
        /// The trainer currently assigned to a client, with their account details.
        /// </summary>
        /// <param name="clientId">The client.</param>
        /// <returns>The trainer, or null when the client is unassigned.</returns>
        Task<TrainerProfile?> GetActiveTrainerAsync(int clientId);

        /// <summary>
        /// BR-08: whether a trainer may see a client, that is, has an active assignment to them.
        /// </summary>
        /// <param name="trainerId">The trainer.</param>
        /// <param name="clientId">The client.</param>
        /// <returns>True when an assignment between them has no end date.</returns>
        Task<bool> IsTrainerAssignedToClientAsync(int trainerId, int clientId);

        /// <summary>
        /// Active client accounts with no active assignment, by name.
        /// </summary>
        /// <returns>The clients, with their account details.</returns>
        Task<IReadOnlyList<ClientProfile>> GetUnassignedClientsAsync();

        /// <summary>
        /// Assigns a trainer to a client (UC31). The trainer must be approved and active with room (BR-07); the client's
        /// current assignment ends the day the new one starts, in the same transaction (BR-06). Client and trainer are told.
        /// </summary>
        /// <param name="clientId">The client.</param>
        /// <param name="trainerId">The trainer.</param>
        /// <param name="startDate">The first day; today or earlier.</param>
        /// <param name="notes">A note on the match.</param>
        /// <param name="actingAdminId">The administrator making the assignment.</param>
        /// <returns>False when the client or trainer doesn't exist.</returns>
        /// <exception cref="Helpers.BusinessRuleException">A BR-06 or BR-07 rule is broken.</exception>
        Task<bool> AssignTrainerAsync(int clientId, int trainerId, DateTime startDate, string? notes, string actingAdminId);

        /// <summary>
        /// Ends an active assignment; the row stays as history (BR-06). Client and trainer are told.
        /// </summary>
        /// <param name="assignmentId">The assignment.</param>
        /// <param name="endDate">The last day; today or earlier, and not before the start.</param>
        /// <param name="actingAdminId">The administrator ending it.</param>
        /// <returns>False when the assignment doesn't exist.</returns>
        /// <exception cref="Helpers.BusinessRuleException">The assignment has ended already, or the date is wrong (BR-06).</exception>
        Task<bool> EndAssignmentAsync(int assignmentId, DateTime endDate, string actingAdminId);

        /// <summary>
        /// One page of assignments, active first, then newest.
        /// </summary>
        /// <param name="filter">Optional name search and active-only filter.</param>
        /// <param name="page">The 1-based page.</param>
        /// <returns>The page.</returns>
        Task<PagedResult<AssignmentListItemViewModel>> GetAssignmentsAsync(AssignmentFilterViewModel filter, int page);

        /// <summary>
        /// Each approved trainer's active clients against their capacity (BR-07), by name.
        /// </summary>
        /// <returns>The trainers' load.</returns>
        Task<IReadOnlyList<TrainerLoadViewModel>> GetTrainerLoadAsync();

        /// <summary>
        /// Everything the assignments page shows: a page of assignments, unassigned clients and trainer load.
        /// </summary>
        /// <param name="filter">The assignment list filters.</param>
        /// <param name="page">The 1-based page of assignments.</param>
        /// <returns>The page's view model.</returns>
        Task<AssignmentsOverviewViewModel> GetOverviewAsync(AssignmentFilterViewModel filter, int page);

        /// <summary>
        /// A new assignment form starting today, optionally for one client, with the choices filled in.
        /// </summary>
        /// <param name="clientId">The client to pre-select, if any.</param>
        /// <returns>The form.</returns>
        Task<AssignTrainerViewModel> GetAssignFormAsync(int? clientId);

        /// <summary>
        /// Fills the form's client and trainer choices: active clients, and approved active trainers with room (BR-07).
        /// </summary>
        /// <param name="model">The form to fill, for example after a failed post.</param>
        /// <returns>A task that completes when the choices are filled.</returns>
        Task FillAssignOptionsAsync(AssignTrainerViewModel model);
    }
}
