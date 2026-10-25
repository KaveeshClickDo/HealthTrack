using System.ComponentModel.DataAnnotations;
using HealthTrack.Models.Enums;

namespace HealthTrack.Models.Entities
{
    /// <summary>
    /// One in-app message or reminder for a user.
    /// </summary>
    public class Notification
    {
        /// <summary>Primary key.</summary>
        public int NotificationId { get; set; }

        /// <summary>The recipient (R6).</summary>
        [MaxLength(450)]
        public string UserId { get; set; } = string.Empty;

        /// <summary>The kind of notification.</summary>
        public NotificationType Type { get; set; }

        /// <summary>Short title.</summary>
        [MaxLength(100)]
        public string Title { get; set; } = string.Empty;

        /// <summary>The message text.</summary>
        [MaxLength(500)]
        public string Message { get; set; } = string.Empty;

        /// <summary>A local URL only, for example /Client/Appointments/Details/12.</summary>
        [MaxLength(200)]
        public string? LinkUrl { get; set; }

        /// <summary>Identifies a reminder, for example MEAL-42-20261025; unique when set, so each reminder is created once (BR-19).</summary>
        [MaxLength(100)]
        public string? ReminderKey { get; set; }

        /// <summary>Whether the recipient has read it.</summary>
        public bool IsRead { get; set; }

        /// <summary>When the notification was created.</summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>When it was read.</summary>
        public DateTime? ReadAt { get; set; }

        /// <summary>When an email copy was sent.</summary>
        public DateTime? EmailSentAt { get; set; }

        /// <summary>The recipient (R6).</summary>
        public virtual ApplicationUser User { get; set; } = null!;

        /// <summary>
        /// Marks the notification as read. Reading it again keeps the first ReadAt.
        /// </summary>
        /// <param name="now">The current time.</param>
        public void MarkAsRead(DateTime now)
        {
            if (IsRead)
            {
                return;
            }

            IsRead = true;
            ReadAt = now;
        }
    }
}
