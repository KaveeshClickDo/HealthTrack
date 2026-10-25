using System.ComponentModel.DataAnnotations;
using HealthTrack.Models.Enums;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// The form for adding or editing an exercise library entry (UC32). No ID means a new exercise.
    /// </summary>
    public class ExerciseViewModel
    {
        /// <summary>The exercise being edited; null for a new one.</summary>
        public int? ExerciseId { get; set; }

        /// <summary>Unique name.</summary>
        [Required(ErrorMessage = "Enter a name.")]
        [StringLength(100)]
        [Display(Name = "Name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>The type of training.</summary>
        [Required(ErrorMessage = "Choose a category.")]
        [Display(Name = "Category")]
        public ExerciseCategory? Category { get; set; }

        /// <summary>The main muscle group.</summary>
        [Required(ErrorMessage = "Choose a muscle group.")]
        [Display(Name = "Muscle group")]
        public MuscleGroup? MuscleGroup { get; set; }

        /// <summary>Equipment needed; empty means body weight only.</summary>
        [StringLength(100)]
        [Display(Name = "Equipment")]
        public string? Equipment { get; set; }

        /// <summary>Short instructions.</summary>
        [StringLength(1000)]
        [Display(Name = "Instructions")]
        public string? Instructions { get; set; }

        /// <summary>MET value, 1.0–20.0, from the Compendium of Physical Activities.</summary>
        [Required(ErrorMessage = "Enter the MET value.")]
        [Range(1.0, 20.0, ErrorMessage = "The MET value must be between 1.0 and 20.0.")]
        [Display(Name = "MET value")]
        public decimal? MetValue { get; set; }

        /// <summary>Whether the exercise is active; shown on the form, changed with the activate/deactivate button.</summary>
        public bool IsActive { get; set; } = true;
    }
}
