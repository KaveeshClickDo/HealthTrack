using HealthTrack.Models.ViewModels;

namespace HealthTrack.Services
{
    /// <summary>
    /// Trainer feedback to clients (UC15, UC22).
    /// </summary>
    public interface IFeedbackService
    {
        /// <summary>
        /// The feedback form, optionally for one client and one of their sessions.
        /// </summary>
        /// <param name="trainerUserId">The signed-in trainer.</param>
        /// <param name="clientId">The client; must be assigned to the trainer (BR-08).</param>
        /// <param name="workoutLogId">The session; must be the client's own.</param>
        /// <returns>The form, or null when the client or session isn't available to the trainer.</returns>
        Task<FeedbackViewModel?> GetFeedbackFormAsync(string trainerUserId, int? clientId, int? workoutLogId);

        /// <summary>
        /// Fills the form's client and session choices, for example after a failed post.
        /// </summary>
        /// <param name="model">The form.</param>
        /// <param name="trainerUserId">The signed-in trainer.</param>
        /// <returns>A task that completes when the choices are filled.</returns>
        Task FillFeedbackOptionsAsync(FeedbackViewModel model, string trainerUserId);

        /// <summary>
        /// Saves feedback for an assigned client and tells them (UC22). An optional session must be the client's own.
        /// </summary>
        /// <param name="trainerUserId">The signed-in trainer.</param>
        /// <param name="model">The validated form.</param>
        /// <returns>The new feedback's ID, or null when the client or session isn't available to the trainer.</returns>
        Task<int?> GiveFeedbackAsync(string trainerUserId, FeedbackViewModel model);

        /// <summary>
        /// One page of a client's feedback, newest first (UC15).
        /// </summary>
        /// <param name="clientId">The client.</param>
        /// <param name="page">The 1-based page.</param>
        /// <returns>The page.</returns>
        Task<PagedResult<FeedbackItemViewModel>> GetForClientAsync(int clientId, int page);
    }
}
