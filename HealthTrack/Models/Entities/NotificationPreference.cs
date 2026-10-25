using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models.Entities
{
    /// <summary>
    /// A user's reminder settings; shares its key with the user (1:1). The defaults follow spec 2.8.
    /// </summary>
    public class NotificationPreference
    {
        /// <summary>Primary key and the user it belongs to (R5).</summary>
        [Key]
        [MaxLength(450)]
        public string UserId { get; set; } = string.Empty;

        /// <summary>Whether workout reminders are on. Default true.</summary>
        public bool WorkoutRemindersEnabled { get; set; } = true;

        /// <summary>From when on a training day the workout reminder may appear. Default 07:00.</summary>
        public TimeSpan WorkoutReminderTime { get; set; } = new(7, 0, 0);

        /// <summary>Whether meal reminders are on. Default true.</summary>
        public bool MealRemindersEnabled { get; set; } = true;

        /// <summary>How many minutes before a planned meal to remind, 0–120. Default 15.</summary>
        public int MealReminderLeadMinutes { get; set; } = 15;

        /// <summary>Whether appointment reminders are on. Default true.</summary>
        public bool AppointmentRemindersEnabled { get; set; } = true;

        /// <summary>How many hours before an appointment to remind, 1–72. Default 24.</summary>
        public int AppointmentReminderLeadHours { get; set; } = 24;

        /// <summary>Whether notifications are also sent by email. Default false.</summary>
        public bool EmailEnabled { get; set; }

        /// <summary>The user the settings belong to (R5).</summary>
        public virtual ApplicationUser User { get; set; } = null!;

        /// <summary>
        /// The default settings for a new user, created at registration.
        /// </summary>
        /// <param name="userId">The new user's ID.</param>
        /// <returns>Unsaved preferences with the spec defaults.</returns>
        public static NotificationPreference CreateDefault(string userId) => new() { UserId = userId };
    }
}
