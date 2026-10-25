using System.ComponentModel.DataAnnotations;
using HealthTrack.Models.Entities;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// The notification settings form (UC06): each reminder type on or off, its time or lead time, and email copies.
    /// The ranges follow spec 2.8.
    /// </summary>
    public class ReminderPreferencesViewModel
    {
        /// <summary>Whether workout reminders are on.</summary>
        [Display(Name = "Remind me on training days")]
        public bool WorkoutRemindersEnabled { get; set; }

        /// <summary>From when on a training day the workout reminder may appear.</summary>
        [Required(ErrorMessage = "Enter a reminder time.")]
        [DataType(DataType.Time)]
        [Display(Name = "From")]
        public TimeSpan? WorkoutReminderTime { get; set; }

        /// <summary>Whether meal reminders are on.</summary>
        [Display(Name = "Remind me before planned meals")]
        public bool MealRemindersEnabled { get; set; }

        /// <summary>Minutes before a planned meal, 0–120.</summary>
        [Required(ErrorMessage = "Enter how many minutes before.")]
        [Range(NotificationPreference.MinMealLeadMinutes, NotificationPreference.MaxMealLeadMinutes,
            ErrorMessage = "Choose between 0 and 120 minutes.")]
        [Display(Name = "Minutes before")]
        public int? MealReminderLeadMinutes { get; set; }

        /// <summary>Whether appointment reminders are on.</summary>
        [Display(Name = "Remind me before confirmed appointments")]
        public bool AppointmentRemindersEnabled { get; set; }

        /// <summary>Hours before an appointment, 1–72.</summary>
        [Required(ErrorMessage = "Enter how many hours before.")]
        [Range(NotificationPreference.MinAppointmentLeadHours, NotificationPreference.MaxAppointmentLeadHours,
            ErrorMessage = "Choose between 1 and 72 hours.")]
        [Display(Name = "Hours before")]
        public int? AppointmentReminderLeadHours { get; set; }

        /// <summary>Whether every notification is also sent by email.</summary>
        [Display(Name = "Also send me each notification by email")]
        public bool EmailEnabled { get; set; }

        /// <summary>The address email copies go to (display only).</summary>
        public string? Email { get; set; }

        /// <summary>Whether workout and meal reminders apply: only clients have plans (display only).</summary>
        public bool ShowsPlanReminders { get; set; }

        /// <summary>Whether appointment reminders apply: clients and professionals (display only).</summary>
        public bool ShowsAppointmentReminders { get; set; }
    }
}
