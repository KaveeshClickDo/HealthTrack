using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// Filters for the exercise and food library lists.
    /// </summary>
    /// <typeparam name="TCategory">ExerciseCategory or FoodCategory.</typeparam>
    public class LibraryFilterViewModel<TCategory> where TCategory : struct, Enum
    {
        /// <summary>Part of the name.</summary>
        [StringLength(100)]
        [Display(Name = "Name")]
        public string? Search { get; set; }

        /// <summary>Only this category.</summary>
        [Display(Name = "Category")]
        public TCategory? Category { get; set; }

        /// <summary>Also show deactivated entries.</summary>
        [Display(Name = "Show inactive")]
        public bool IncludeInactive { get; set; }
    }
}
