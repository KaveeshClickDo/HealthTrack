using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models.Enums
{
    /// <summary>
    /// A client's main goal. It adjusts the suggested calorie target (spec 1.10).
    /// </summary>
    public enum FitnessGoal
    {
        /// <summary>Lose weight (TDEE − 500 kcal).</summary>
        [Display(Name = "Lose weight")]
        LoseWeight = 1,

        /// <summary>Keep the current weight (TDEE).</summary>
        [Display(Name = "Maintain weight")]
        MaintainWeight = 2,

        /// <summary>Gain muscle (TDEE + 300 kcal).</summary>
        [Display(Name = "Gain muscle")]
        GainMuscle = 3,

        /// <summary>Improve endurance (TDEE).</summary>
        [Display(Name = "Improve endurance")]
        ImproveEndurance = 4,

        /// <summary>Improve flexibility (TDEE).</summary>
        [Display(Name = "Improve flexibility")]
        ImproveFlexibility = 5,

        /// <summary>General health and wellbeing (TDEE).</summary>
        [Display(Name = "General wellbeing")]
        GeneralWellbeing = 6
    }
}
