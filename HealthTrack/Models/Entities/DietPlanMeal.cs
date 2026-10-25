using System.ComponentModel.DataAnnotations;
using HealthTrack.Models.Enums;

namespace HealthTrack.Models.Entities
{
    /// <summary>
    /// One scheduled meal in a diet plan, for every day or one weekday. Its time drives meal reminders.
    /// </summary>
    public class DietPlanMeal
    {
        /// <summary>Primary key.</summary>
        public int DietPlanMealId { get; set; }

        /// <summary>The plan the meal belongs to (R22).</summary>
        public int DietPlanId { get; set; }

        /// <summary>The weekday the meal is for; null means every day.</summary>
        public DayOfWeek? DayOfWeek { get; set; }

        /// <summary>Breakfast, lunch, dinner or snack.</summary>
        public MealType MealType { get; set; }

        /// <summary>When the meal is planned; drives meal reminders.</summary>
        public TimeSpan ScheduledTime { get; set; }

        /// <summary>What to eat, for example oats 50 g, banana, 200 ml low-fat milk.</summary>
        [MaxLength(500)]
        public string MealDescription { get; set; } = string.Empty;

        /// <summary>Target kcal for the meal, 50–2,000.</summary>
        public int TargetCalories { get; set; }

        /// <summary>The plan the meal belongs to (R22).</summary>
        public virtual DietPlan DietPlan { get; set; } = null!;

        /// <summary>Meals the client logged against this planned meal (R24).</summary>
        public virtual ICollection<MealLog> MealLogs { get; set; } = new List<MealLog>();

        /// <summary>
        /// Whether the meal is planned on a weekday: every-day meals apply to all days.
        /// </summary>
        /// <param name="day">The weekday.</param>
        /// <returns>True when <see cref="DayOfWeek"/> is null or equals <paramref name="day"/>.</returns>
        public bool AppliesTo(DayOfWeek day) => DayOfWeek == null || DayOfWeek == day;
    }
}
