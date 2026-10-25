using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models.Enums
{
    /// <summary>
    /// The kind of in-app notification (spec 1.8). The first three are reminders; the rest are event notifications.
    /// </summary>
    public enum NotificationType
    {
        /// <summary>Today is a training day and nothing is logged yet.</summary>
        [Display(Name = "Workout reminder")]
        WorkoutReminder = 1,

        /// <summary>A planned meal is coming up and is not logged yet.</summary>
        [Display(Name = "Meal reminder")]
        MealReminder = 2,

        /// <summary>A confirmed appointment starts soon.</summary>
        [Display(Name = "Appointment reminder")]
        AppointmentReminder = 3,

        /// <summary>An appointment was requested, approved, rejected, rescheduled or cancelled (BR-18).</summary>
        [Display(Name = "Appointment update")]
        AppointmentUpdate = 4,

        /// <summary>A workout or diet plan was activated for the client.</summary>
        [Display(Name = "Plan assigned")]
        PlanAssigned = 5,

        /// <summary>A trainer left new feedback.</summary>
        [Display(Name = "New feedback")]
        NewFeedback = 6,

        /// <summary>An account event such as an approval or a trainer assignment.</summary>
        [Display(Name = "Account update")]
        AccountUpdate = 7,

        /// <summary>A general system message.</summary>
        [Display(Name = "System")]
        System = 8
    }
}
