using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models.Enums
{
    /// <summary>
    /// The WHO adult BMI category (spec 1.10). Computed from the BMI, never stored.
    /// </summary>
    public enum BmiCategory
    {
        /// <summary>BMI under 18.5.</summary>
        [Display(Name = "Underweight")]
        Underweight = 1,

        /// <summary>BMI 18.5 to 24.9.</summary>
        [Display(Name = "Normal")]
        Normal = 2,

        /// <summary>BMI 25.0 to 29.9.</summary>
        [Display(Name = "Overweight")]
        Overweight = 3,

        /// <summary>BMI 30.0 and over.</summary>
        [Display(Name = "Obese")]
        Obese = 4
    }
}
