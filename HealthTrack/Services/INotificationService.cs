using HealthTrack.Models.Entities;
using HealthTrack.Models.Enums;

namespace HealthTrack.Services
{
    /// <summary>
    /// Creates in-app notifications (spec 1.8). The service that makes a change raises its notifications through this.
    /// </summary>
    public interface INotificationService
    {
        /// <summary>
        /// Saves a notification for a user.
        /// </summary>
        /// <param name="userId">The recipient.</param>
        /// <param name="type">The kind of notification.</param>
        /// <param name="title">A short title, cut to 100 characters.</param>
        /// <param name="message">The message, cut to 500 characters.</param>
        /// <param name="linkUrl">A local path such as /Client/Appointments; never an absolute URL.</param>
        /// <returns>The saved notification.</returns>
        /// <exception cref="ArgumentException"><paramref name="linkUrl"/> is not a local path of up to 200 characters.</exception>
        Task<Notification> NotifyAsync(string userId, NotificationType type, string title, string message, string? linkUrl = null);
    }
}
