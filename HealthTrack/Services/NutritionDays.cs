using HealthTrack.Data;
using HealthTrack.Models.Entities;
using HealthTrack.Models.Enums;
using HealthTrack.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Services
{
    /// <summary>
    /// Day-by-day eating against the diet plan in effect, shared by DietService (summary, history) and ProgressService
    /// (calories chart, trainer view), so both count a day the same way.
    /// </summary>
    internal static class NutritionDays
    {
        /// <summary>
        /// The client's diet plans that have been followed: active or completed, never drafts or archived ones.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="clientId">The client.</param>
        /// <returns>The plans, without meals.</returns>
        public static Task<List<DietPlan>> LoadFollowedPlansAsync(ApplicationDbContext db, int clientId) =>
            db.DietPlans.AsNoTracking()
                .Where(p => p.ClientId == clientId && (p.Status == PlanStatus.Active || p.Status == PlanStatus.Completed))
                .ToListAsync();

        /// <summary>
        /// The plan in effect on a day: started on or before it, not ended before it, and the latest to start, since
        /// activating a new plan completes the old one (BR-09).
        /// </summary>
        /// <param name="plans">The followed plans from <see cref="LoadFollowedPlansAsync"/>.</param>
        /// <param name="date">The day.</param>
        /// <returns>The plan, or null when none applied.</returns>
        public static DietPlan? PlanOn(IEnumerable<DietPlan> plans, DateTime date) =>
            plans.Where(p => p.StartDate <= date.Date && (p.EndDate == null || p.EndDate >= date.Date))
                .OrderByDescending(p => p.StartDate)
                .ThenByDescending(p => p.DietPlanId)
                .FirstOrDefault();

        /// <summary>
        /// One summary per date: the day's meals and totals (spec 1.10: the sum of the day's items) against the targets
        /// of the plan in effect that day.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="clientId">The client.</param>
        /// <param name="dates">The days, in the order wanted.</param>
        /// <returns>The summaries in the same order.</returns>
        public static async Task<IReadOnlyList<DailyNutritionViewModel>> GetDaysAsync(ApplicationDbContext db, int clientId,
            IReadOnlyList<DateTime> dates)
        {
            if (dates.Count == 0)
            {
                return [];
            }

            var first = dates.Min().Date;
            var last = dates.Max().Date;
            var logs = await db.MealLogs.AsNoTracking()
                .Where(m => m.ClientId == clientId && m.MealDate >= first && m.MealDate <= last)
                .Include(m => m.Items).ThenInclude(i => i.FoodItem)
                .Include(m => m.DietPlanMeal)
                .AsSplitQuery()
                .OrderBy(m => m.MealType)
                .ThenBy(m => m.MealLogId)
                .ToListAsync();
            var plans = await LoadFollowedPlansAsync(db, clientId);

            return dates.Select(date =>
            {
                var day = date.Date;
                var meals = logs.Where(m => m.MealDate == day).ToList();
                var plan = PlanOn(plans, day);
                return new DailyNutritionViewModel
                {
                    Date = day,
                    CaloriesEaten = meals.Sum(m => m.TotalCalories),
                    ProteinG = meals.Sum(m => m.TotalProteinG),
                    CarbsG = meals.Sum(m => m.TotalCarbsG),
                    FatG = meals.Sum(m => m.TotalFatG),
                    CalorieTarget = plan?.DailyCalorieTarget,
                    ProteinTargetG = plan?.ProteinTargetG,
                    CarbsTargetG = plan?.CarbsTargetG,
                    FatTargetG = plan?.FatTargetG,
                    Meals = meals.Select(m => new LoggedMealViewModel
                    {
                        MealLogId = m.MealLogId,
                        MealType = m.MealType,
                        PlannedDescription = m.DietPlanMeal?.MealDescription,
                        Notes = m.Notes,
                        Items = m.Items.OrderBy(i => i.MealLogItemId).Select(i => new LoggedFoodViewModel
                        {
                            FoodName = i.FoodItem.Name,
                            Quantity = i.Quantity,
                            ServingSize = i.FoodItem.ServingSize,
                            ServingUnit = i.FoodItem.ServingUnit,
                            Calories = i.Calories
                        }).ToList()
                    }).ToList()
                };
            }).ToList();
        }
    }
}
