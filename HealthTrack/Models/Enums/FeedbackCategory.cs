using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models.Enums
{
    /// <summary>
    /// What a piece of trainer feedback is about.
    /// </summary>
    public enum FeedbackCategory
    {
        /// <summary>A workout or training habits.</summary>
        [Display(Name = "Workout")]
        Workout = 1,

        /// <summary>Eating habits or the diet plan.</summary>
        [Display(Name = "Diet")]
        Diet = 2,

        /// <summary>Overall progress towards the goal.</summary>
        [Display(Name = "Progress")]
        Progress = 3,

        /// <summary>Anything else.</summary>
        [Display(Name = "General")]
        General = 4
    }
}
