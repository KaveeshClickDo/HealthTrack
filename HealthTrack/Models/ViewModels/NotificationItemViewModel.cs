using HealthTrack.Models.Enums;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// One notification in the list, the bell's poll and the toasts.
    /// </summary>
    public class NotificationItemViewModel
    {
        /// <summary>The notification's key.</summary>
        public int NotificationId { get; set; }

        /// <summary>The kind of notification.</summary>
        public NotificationType Type { get; set; }

        /// <summary>The title.</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>The message.</summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>A local link to the page it is about, or null.</summary>
        public string? LinkUrl { get; set; }

        /// <summary>Whether it has been read.</summary>
        public bool IsRead { get; set; }

        /// <summary>When it was created.</summary>
        public DateTime CreatedAt { get; set; }
    }
}
