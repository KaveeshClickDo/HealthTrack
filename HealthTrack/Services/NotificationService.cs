using HealthTrack.Data;
using HealthTrack.Helpers;
using HealthTrack.Models.Entities;
using HealthTrack.Models.Enums;

namespace HealthTrack.Services
{
    /// <summary>
    /// Saves in-app notifications.
    /// </summary>
    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _db;
        private readonly IClock _clock;

        /// <summary>
        /// Creates the service.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="clock">Supplies the time.</param>
        public NotificationService(ApplicationDbContext db, IClock clock)
        {
            _db = db;
            _clock = clock;
        }

        /// <inheritdoc />
        public async Task<Notification> NotifyAsync(string userId, NotificationType type, string title, string message, string? linkUrl = null)
        {
            // A notification link is followed by the browser, so only local paths are allowed (no open redirects).
            if (linkUrl is not null && !IsLocalPath(linkUrl))
            {
                throw new ArgumentException("A notification link must be a local path of up to 200 characters, such as /Client/Appointments.", nameof(linkUrl));
            }

            var notification = new Notification
            {
                UserId = userId,
                Type = type,
                Title = title.Truncate(100)!,
                Message = message.Truncate(500)!,
                LinkUrl = linkUrl,
                CreatedAt = _clock.Now
            };

            _db.Notifications.Add(notification);
            await _db.SaveChangesAsync();
            return notification;
        }

        // The same rule as Url.IsLocalUrl for paths: starts with one "/", not "//" or "/\".
        private static bool IsLocalPath(string url) =>
            url.Length is > 0 and <= 200
            && url[0] == '/'
            && (url.Length == 1 || (url[1] != '/' && url[1] != '\\'));
    }
}
