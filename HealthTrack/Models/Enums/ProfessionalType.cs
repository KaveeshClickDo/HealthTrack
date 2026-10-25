using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models.Enums
{
    /// <summary>
    /// The kind of professional behind a Trainer account (spec 1.2).
    /// </summary>
    public enum ProfessionalType
    {
        /// <summary>Fitness trainer; the only type that needs no licence number (BR-03).</summary>
        [Display(Name = "Fitness trainer")]
        FitnessTrainer = 1,

        /// <summary>Nutritionist or dietitian.</summary>
        [Display(Name = "Nutritionist")]
        Nutritionist = 2,

        /// <summary>Physiotherapist.</summary>
        [Display(Name = "Physiotherapist")]
        Physiotherapist = 3,

        /// <summary>Medical doctor.</summary>
        [Display(Name = "Medical doctor")]
        MedicalDoctor = 4
    }
}
