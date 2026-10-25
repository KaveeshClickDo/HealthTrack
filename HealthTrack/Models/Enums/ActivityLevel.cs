using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models.Enums
{
    /// <summary>
    /// How active a client is day to day. It sets the TDEE activity factor (spec 1.10).
    /// </summary>
    public enum ActivityLevel
    {
        /// <summary>Little or no exercise (× 1.2).</summary>
        [Display(Name = "Sedentary")]
        Sedentary = 1,

        /// <summary>Light exercise 1–3 days a week (× 1.375).</summary>
        [Display(Name = "Lightly active")]
        LightlyActive = 2,

        /// <summary>Moderate exercise 3–5 days a week (× 1.55).</summary>
        [Display(Name = "Moderately active")]
        ModeratelyActive = 3,

        /// <summary>Hard exercise 6–7 days a week (× 1.725).</summary>
        [Display(Name = "Very active")]
        VeryActive = 4,

        /// <summary>Very hard exercise or a physical job (× 1.9).</summary>
        [Display(Name = "Extra active")]
        ExtraActive = 5
    }
}
