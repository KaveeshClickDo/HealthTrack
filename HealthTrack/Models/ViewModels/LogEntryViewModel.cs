using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// One exercise row in the workout log form. Rows that aren't ticked as done are ignored.
    /// </summary>
    public class LogEntryViewModel
    {
        /// <summary>
        /// Whether the exercise was done; only ticked rows are saved. False by default because an unticked checkbox
        /// posts nothing, so binding leaves the default in place; the forms tick their rows explicitly.
        /// </summary>
        [Display(Name = "Done")]
        public bool Include { get; set; }

        /// <summary>The exercise.</summary>
        [Required(ErrorMessage = "Choose an exercise.")]
        [Display(Name = "Exercise")]
        public int? ExerciseId { get; set; }

        /// <summary>Minutes spent, 1–300; drives the calories (BR-14).</summary>
        [Required(ErrorMessage = "Enter the minutes.")]
        [Range(1, 300, ErrorMessage = "Minutes must be between 1 and 300.")]
        [Display(Name = "Minutes")]
        public int? DurationMinutes { get; set; }

        /// <summary>Sets completed.</summary>
        [Range(1, 20, ErrorMessage = "Sets must be between 1 and 20.")]
        [Display(Name = "Sets")]
        public int? Sets { get; set; }

        /// <summary>Reps per set.</summary>
        [Range(1, 100, ErrorMessage = "Reps must be between 1 and 100.")]
        [Display(Name = "Reps")]
        public int? Reps { get; set; }

        /// <summary>Load lifted in kg.</summary>
        [Range(0.0, 500.0, ErrorMessage = "The load must be between 0 and 500 kg.")]
        [Display(Name = "Load (kg)")]
        public decimal? LoadKg { get; set; }

        /// <summary>Distance in km, for cardio.</summary>
        [Range(0.0, 100.0, ErrorMessage = "The distance must be between 0 and 100 km.")]
        [Display(Name = "Distance (km)")]
        public decimal? DistanceKm { get; set; }

        /// <summary>The planned prescription, shown as a reminder on today's rows.</summary>
        [StringLength(100)]
        public string? Target { get; set; }
    }
}
