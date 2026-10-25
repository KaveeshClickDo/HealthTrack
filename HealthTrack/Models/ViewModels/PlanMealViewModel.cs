using System.ComponentModel.DataAnnotations;
using HealthTrack.Models.Enums;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// The form for adding a meal to a diet plan, for every day or one weekday.
    /// </summary>
    public class PlanMealViewModel
    {
        /// <summary>The weekday; empty means every day.</summary>
        [Display(Name = "Day")]
        public DayOfWeek? DayOfWeek { get; set; }

        /// <summary>Breakfast, lunch, dinner or snack.</summary>
        [Required(ErrorMessage = "Choose the meal.")]
        [Display(Name = "Meal")]
        public MealType? MealType { get; set; }

        /// <summary>When the meal is planned; drives meal reminders.</summary>
        [Required(ErrorMessage = "Enter a time.")]
        [DataType(DataType.Time)]
        [Display(Name = "Time")]
        public TimeSpan? ScheduledTime { get; set; }

        /// <summary>What to eat.</summary>
        [Required(ErrorMessage = "Describe the meal.")]
        [StringLength(500)]
        [Display(Name = "What to eat")]
        public string MealDescription { get; set; } = string.Empty;

        /// <summary>Target kcal, 50–2,000.</summary>
        [Required(ErrorMessage = "Enter the target calories.")]
        [Range(50, 2000, ErrorMessage = "A meal's target must be between 50 and 2,000 kcal.")]
        [Display(Name = "Target (kcal)")]
        public int? TargetCalories { get; set; }
    }
}
