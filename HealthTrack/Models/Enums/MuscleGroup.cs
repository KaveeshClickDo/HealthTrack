using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models.Enums
{
    /// <summary>
    /// The main muscle group an exercise works.
    /// </summary>
    public enum MuscleGroup
    {
        /// <summary>Works the whole body.</summary>
        [Display(Name = "Full body")]
        FullBody = 1,

        /// <summary>Chest.</summary>
        [Display(Name = "Chest")]
        Chest = 2,

        /// <summary>Back.</summary>
        [Display(Name = "Back")]
        Back = 3,

        /// <summary>Shoulders.</summary>
        [Display(Name = "Shoulders")]
        Shoulders = 4,

        /// <summary>Arms.</summary>
        [Display(Name = "Arms")]
        Arms = 5,

        /// <summary>Core and abdominals.</summary>
        [Display(Name = "Core")]
        Core = 6,

        /// <summary>Legs and glutes.</summary>
        [Display(Name = "Legs")]
        Legs = 7
    }
}
