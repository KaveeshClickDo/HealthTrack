using HealthTrack.Helpers;
using HealthTrack.Models.ViewModels;
using HealthTrack.Services;
using Microsoft.AspNetCore.Mvc;

namespace HealthTrack.Areas.Client.Controllers
{
    /// <summary>
    /// The client's diet: the active plan, meal logging and the daily history (UC08, UC10).
    /// </summary>
    public class DietController : ClientAreaController
    {
        private readonly IDietService _diet;

        /// <summary>
        /// Creates the controller.
        /// </summary>
        /// <param name="diet">The diet service.</param>
        public DietController(IDietService diet)
        {
            _diet = diet;
        }

        /// <summary>
        /// The active diet plan: targets, today's meals and the week's schedule.
        /// </summary>
        /// <returns>The plan.</returns>
        [HttpGet]
        public async Task<IActionResult> MyPlan()
        {
            if (await CurrentClientIdAsync() is not { } clientId)
            {
                return NotFound();
            }

            return View(await _diet.GetWeekAsync(clientId));
        }

        /// <summary>
        /// The meal log form, for a planned meal or an unplanned one.
        /// </summary>
        /// <param name="dietPlanMealId">The planned meal being logged, if any.</param>
        /// <returns>The form, or 404 for a meal that isn't in the client's active plan.</returns>
        [HttpGet]
        public async Task<IActionResult> LogMeal(int? dietPlanMealId)
        {
            if (await CurrentClientIdAsync() is not { } clientId)
            {
                return NotFound();
            }

            var model = await _diet.GetLogMealFormAsync(clientId, dietPlanMealId);
            return model is null ? NotFound() : View(model);
        }

        /// <summary>
        /// Saves the meal; calories and macros are worked out from the food library.
        /// </summary>
        /// <param name="model">The form.</param>
        /// <returns>The history, or the form with its errors.</returns>
        [HttpPost]
        public async Task<IActionResult> LogMeal(LogMealViewModel model)
        {
            if (await CurrentClientIdAsync() is not { } clientId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    if (await _diet.LogMealAsync(CurrentUserId, model) is null)
                    {
                        return NotFound();
                    }

                    SetSuccess("Your meal has been saved. Calories and macros were worked out from the food library.");
                    return RedirectToAction(model.DietPlanMealId is null ? nameof(History) : nameof(MyPlan));
                }
                catch (BusinessRuleException ex)
                {
                    ModelState.AddModelError(string.Empty, ex.Message);
                }
            }

            await _diet.FillLogMealOptionsAsync(model, clientId);
            return View(model);
        }

        /// <summary>
        /// The days with logged meals, each with totals against the target, 20 days per page.
        /// </summary>
        /// <param name="page">The page number.</param>
        /// <returns>The history.</returns>
        [HttpGet]
        public async Task<IActionResult> History(int page = 1)
        {
            if (await CurrentClientIdAsync() is not { } clientId)
            {
                return NotFound();
            }

            return View(await _diet.GetHistoryAsync(clientId, page));
        }
    }
}
