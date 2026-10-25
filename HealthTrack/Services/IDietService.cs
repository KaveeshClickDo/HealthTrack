using HealthTrack.Models.ViewModels;

namespace HealthTrack.Services
{
    /// <summary>
    /// Diet plans and meal logging (UC08, UC10, UC21, UC36). Trainer methods take the trainer's user ID and answer
    /// null or false for a plan or client the trainer may not reach (BR-08).
    /// </summary>
    public interface IDietService
    {
        /// <summary>
        /// The new diet plan form. With a client chosen, the calorie target is pre-filled from the client's suggested
        /// target (spec 1.10), kept within 1,200–5,000 kcal.
        /// </summary>
        /// <param name="trainerUserId">The signed-in trainer's user ID.</param>
        /// <param name="clientId">The chosen client, or null to choose one first.</param>
        /// <returns>The form, or null when the client isn't assigned to the trainer.</returns>
        Task<DietPlanViewModel?> GetCreatePlanFormAsync(string trainerUserId, int? clientId);

        /// <summary>
        /// Refills the form's client list, client name and suggested target after a failed post.
        /// </summary>
        /// <param name="model">The posted form.</param>
        /// <param name="trainerUserId">The signed-in trainer's user ID.</param>
        /// <returns>A task.</returns>
        Task FillPlanOptionsAsync(DietPlanViewModel model, string trainerUserId);

        /// <summary>
        /// Creates a Draft diet plan for an assigned client.
        /// </summary>
        /// <param name="trainerUserId">The signed-in trainer's user ID.</param>
        /// <param name="model">The form.</param>
        /// <returns>The new plan's key, or null when the client isn't assigned to the trainer (BR-08).</returns>
        /// <exception cref="Helpers.BusinessRuleException">BR-10: the dates are wrong.</exception>
        Task<int?> CreatePlanAsync(string trainerUserId, DietPlanViewModel model);

        /// <summary>
        /// The trainer's diet plans, current ones first, 20 per page.
        /// </summary>
        /// <param name="trainerUserId">The signed-in trainer's user ID.</param>
        /// <param name="page">The page number.</param>
        /// <returns>The page.</returns>
        Task<PagedResult<DietPlanListItemViewModel>> GetTrainerPlansAsync(string trainerUserId, int page);

        /// <summary>
        /// The diet plan editor for the plan's author, while the client is still assigned.
        /// </summary>
        /// <param name="planId">The plan.</param>
        /// <param name="trainerUserId">The signed-in trainer's user ID.</param>
        /// <returns>The editor, or null when the trainer may not reach the plan.</returns>
        Task<DietPlanEditViewModel?> GetPlanForEditAsync(int planId, string trainerUserId);

        /// <summary>
        /// Refills the editor's display-only values after a failed post.
        /// </summary>
        /// <param name="model">The posted editor.</param>
        /// <param name="planId">The plan.</param>
        /// <param name="trainerUserId">The signed-in trainer's user ID.</param>
        /// <returns>False when the trainer may not reach the plan.</returns>
        Task<bool> FillEditDisplayAsync(DietPlanEditViewModel model, int planId, string trainerUserId);

        /// <summary>
        /// Saves the details and meal changes, removing the ticked meals.
        /// </summary>
        /// <param name="planId">The plan.</param>
        /// <param name="trainerUserId">The signed-in trainer's user ID.</param>
        /// <param name="model">The editor.</param>
        /// <returns>False when the trainer may not reach the plan.</returns>
        /// <exception cref="Helpers.BusinessRuleException">
        /// BR-21: the plan is completed, or a ticked meal has been logged; BR-10: the dates are wrong, or an active plan
        /// would have no meals.
        /// </exception>
        Task<bool> UpdatePlanAsync(int planId, string trainerUserId, DietPlanEditViewModel model);

        /// <summary>
        /// Adds a scheduled meal to the plan.
        /// </summary>
        /// <param name="planId">The plan.</param>
        /// <param name="trainerUserId">The signed-in trainer's user ID.</param>
        /// <param name="model">The meal.</param>
        /// <returns>False when the trainer may not reach the plan.</returns>
        /// <exception cref="Helpers.BusinessRuleException">BR-21: the plan is completed.</exception>
        Task<bool> AddMealAsync(int planId, string trainerUserId, PlanMealViewModel model);

        /// <summary>
        /// Activates a draft plan: it needs a meal (BR-10), the client's previous active diet plan is completed (BR-09),
        /// and the client gets a PlanAssigned notification.
        /// </summary>
        /// <param name="planId">The plan.</param>
        /// <param name="trainerUserId">The signed-in trainer's user ID.</param>
        /// <returns>False when the trainer may not reach the plan.</returns>
        /// <exception cref="Helpers.BusinessRuleException">BR-09 or BR-10.</exception>
        Task<bool> ActivatePlanAsync(int planId, string trainerUserId);

        /// <summary>
        /// The client's active diet plan: targets, today's meals and eating, and the Monday–Sunday schedule (UC08).
        /// </summary>
        /// <param name="clientId">The client.</param>
        /// <returns>The plan, or one with HasPlan false.</returns>
        Task<DietWeekViewModel> GetWeekAsync(int clientId);

        /// <summary>
        /// Today's planned meals from the active diet plan, each marked logged or not; empty when no plan applies today.
        /// </summary>
        /// <param name="clientId">The client.</param>
        /// <param name="today">Today's date.</param>
        /// <returns>The meals by time.</returns>
        Task<IReadOnlyList<PlannedMealViewModel>> GetTodaysMealsAsync(int clientId, DateTime today);

        /// <summary>
        /// The meal log form, for a planned meal of the client's active plan or an unplanned meal.
        /// </summary>
        /// <param name="clientId">The client.</param>
        /// <param name="dietPlanMealId">The planned meal, or null.</param>
        /// <returns>The form, or null when the planned meal isn't in the client's active plan.</returns>
        Task<LogMealViewModel?> GetLogMealFormAsync(int clientId, int? dietPlanMealId);

        /// <summary>
        /// Refills the food picker and the planned meal's details after a failed post.
        /// </summary>
        /// <param name="model">The posted form.</param>
        /// <param name="clientId">The client.</param>
        /// <returns>A task.</returns>
        Task FillLogMealOptionsAsync(LogMealViewModel model, int clientId);

        /// <summary>
        /// Saves a meal for the signed-in client. Each item's calories and macros are quantity × the food's per-serving
        /// values, stored as a snapshot (UC36, BR-14).
        /// </summary>
        /// <param name="clientUserId">The signed-in client's user ID.</param>
        /// <param name="model">The form.</param>
        /// <returns>The new log's key, or null when the planned meal isn't the client's.</returns>
        /// <exception cref="Helpers.BusinessRuleException">
        /// BR-13: a future date or no foods; BR-21: a food is no longer in the library.
        /// </exception>
        Task<int?> LogMealAsync(string clientUserId, LogMealViewModel model);

        /// <summary>
        /// One day's eating against the targets of the plan in effect that day, with its meals.
        /// </summary>
        /// <param name="clientId">The client.</param>
        /// <param name="date">The day.</param>
        /// <returns>The summary.</returns>
        Task<DailyNutritionViewModel> GetDailySummaryAsync(int clientId, DateTime date);

        /// <summary>
        /// The days the client logged meals, newest first, each with totals against the target, 20 days per page.
        /// </summary>
        /// <param name="clientId">The client.</param>
        /// <param name="page">The page number.</param>
        /// <returns>The page.</returns>
        Task<PagedResult<DailyNutritionViewModel>> GetHistoryAsync(int clientId, int page);
    }
}
