using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models.Enums
{
    /// <summary>
    /// The meal of the day a planned or logged meal belongs to.
    /// </summary>
    public enum MealType
    {
        /// <summary>Breakfast.</summary>
        [Display(Name = "Breakfast")]
        Breakfast = 1,

        /// <summary>Lunch.</summary>
        [Display(Name = "Lunch")]
        Lunch = 2,

        /// <summary>Dinner.</summary>
        [Display(Name = "Dinner")]
        Dinner = 3,

        /// <summary>A snack between meals.</summary>
        [Display(Name = "Snack")]
        Snack = 4
    }
}
