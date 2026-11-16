using HealthTrack.Models.ViewModels;

namespace HealthTrack.Services
{
    /// <summary>
    /// The small counts the sidebar shows next to its links (the design's nav badges).
    /// </summary>
    public interface INavigationService
    {
        /// <summary>
        /// Counts for the signed-in user's role: a trainer's active clients and pending requests, or the trainers
        /// awaiting an administrator's approval. Clients get no counts.
        /// </summary>
        /// <param name="userId">The signed-in user's id.</param>
        /// <param name="isTrainer">Whether the user is a trainer.</param>
        /// <param name="isAdmin">Whether the user is an administrator.</param>
        /// <returns>The counts; zero where they don't apply.</returns>
        Task<NavCountsViewModel> GetCountsAsync(string userId, bool isTrainer, bool isAdmin);
    }
}
