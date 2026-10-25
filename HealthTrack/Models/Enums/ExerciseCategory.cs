using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models.Enums
{
    /// <summary>
    /// The type of training an exercise belongs to.
    /// </summary>
    public enum ExerciseCategory
    {
        /// <summary>Steady aerobic work such as running or cycling.</summary>
        [Display(Name = "Cardio")]
        Cardio = 1,

        /// <summary>Resistance training with weights or body weight.</summary>
        [Display(Name = "Strength")]
        Strength = 2,

        /// <summary>Stretching, yoga and mobility work.</summary>
        [Display(Name = "Flexibility")]
        Flexibility = 3,

        /// <summary>High-intensity interval training.</summary>
        [Display(Name = "HIIT")]
        HIIT = 4,

        /// <summary>Balance and stability training.</summary>
        [Display(Name = "Balance")]
        Balance = 5,

        /// <summary>Recreational and competitive sports.</summary>
        [Display(Name = "Sports")]
        Sports = 6
    }
}
