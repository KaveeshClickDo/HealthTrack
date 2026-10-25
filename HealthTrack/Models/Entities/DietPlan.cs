using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HealthTrack.Helpers;
using HealthTrack.Models.Enums;

namespace HealthTrack.Models.Entities
{
    /// <summary>
    /// A client's daily calorie and macro targets with the meals scheduled to meet them.
    /// </summary>
    public class DietPlan
    {
        /// <summary>Primary key.</summary>
        public int DietPlanId { get; set; }

        /// <summary>The trainer who wrote the plan (R20).</summary>
        public int TrainerId { get; set; }

        /// <summary>The client following the plan (R21).</summary>
        public int ClientId { get; set; }

        /// <summary>Title.</summary>
        [MaxLength(100)]
        public string Title { get; set; } = string.Empty;

        /// <summary>Guidance for the client.</summary>
        [MaxLength(1000)]
        public string? Description { get; set; }

        /// <summary>Daily calorie target, 1,200–5,000 kcal.</summary>
        public int DailyCalorieTarget { get; set; }

        /// <summary>Daily protein target in grams.</summary>
        public int? ProteinTargetG { get; set; }

        /// <summary>Daily carbohydrate target in grams.</summary>
        public int? CarbsTargetG { get; set; }

        /// <summary>Daily fat target in grams.</summary>
        public int? FatTargetG { get; set; }

        /// <summary>First day of the plan.</summary>
        [Column(TypeName = "date")]
        public DateTime StartDate { get; set; }

        /// <summary>Last day of the plan; on or after <see cref="StartDate"/> (BR-10).</summary>
        [Column(TypeName = "date")]
        public DateTime? EndDate { get; set; }

        /// <summary>Where the plan is in its lifecycle; new plans start as drafts.</summary>
        public PlanStatus Status { get; set; } = PlanStatus.Draft;

        /// <summary>When the plan was created.</summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>When the plan was last changed.</summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>The trainer who wrote the plan (R20).</summary>
        public virtual TrainerProfile Trainer { get; set; } = null!;

        /// <summary>The client following the plan (R21).</summary>
        public virtual ClientProfile Client { get; set; } = null!;

        /// <summary>The scheduled meals (R22).</summary>
        public virtual ICollection<DietPlanMeal> Meals { get; set; } = new List<DietPlanMeal>();

        /// <summary>
        /// The meals planned for one weekday: every-day meals plus that day's own, in time order.
        /// </summary>
        /// <param name="day">The weekday.</param>
        /// <returns>The day's meals ordered by ScheduledTime.</returns>
        public List<DietPlanMeal> GetMealsFor(DayOfWeek day) =>
            Meals.Where(m => m.AppliesTo(day)).OrderBy(m => m.ScheduledTime).ToList();

        /// <summary>
        /// Whether the plan is active and <paramref name="date"/> falls between its start and end dates.
        /// </summary>
        /// <param name="date">The date to check (the time part is ignored).</param>
        /// <returns>True when the plan applies on that date.</returns>
        public bool IsActiveOn(DateTime date) =>
            Status == PlanStatus.Active && date.Date >= StartDate && (EndDate == null || date.Date <= EndDate);

        /// <summary>
        /// Moves a draft plan to Active. It needs at least one meal (BR-10). The service completes the client's
        /// previous active diet plan in the same transaction (BR-09).
        /// </summary>
        /// <exception cref="BusinessRuleException">The plan is not a draft, or has no meals.</exception>
        public void Activate()
        {
            if (Status != PlanStatus.Draft)
            {
                throw new BusinessRuleException("BR-09", "Only a draft plan can be activated.");
            }

            if (Meals.Count == 0)
            {
                throw new BusinessRuleException("BR-10", "Add at least one meal before activating the plan.");
            }

            Status = PlanStatus.Active;
        }

        /// <summary>
        /// Moves an active plan to Completed, for example when a newer plan is activated for the client (BR-09).
        /// </summary>
        /// <exception cref="BusinessRuleException">The plan is not active.</exception>
        public void Complete()
        {
            if (Status != PlanStatus.Active)
            {
                throw new BusinessRuleException("BR-09", "Only an active plan can be completed.");
            }

            Status = PlanStatus.Completed;
        }
    }
}
