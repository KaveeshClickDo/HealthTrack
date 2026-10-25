using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models.Enums
{
    /// <summary>
    /// How demanding a workout plan is.
    /// </summary>
    public enum DifficultyLevel
    {
        /// <summary>Suitable for newcomers.</summary>
        [Display(Name = "Beginner")]
        Beginner = 1,

        /// <summary>For clients who train regularly.</summary>
        [Display(Name = "Intermediate")]
        Intermediate = 2,

        /// <summary>For experienced clients.</summary>
        [Display(Name = "Advanced")]
        Advanced = 3
    }
}
