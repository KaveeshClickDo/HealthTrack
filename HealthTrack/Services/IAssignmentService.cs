using HealthTrack.Models.Entities;

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
    }
}
