using HealthTrack.Helpers;
using HealthTrack.Models.ViewModels;
using HealthTrack.Services;
using Microsoft.AspNetCore.Mvc;

namespace HealthTrack.Areas.Trainer.Controllers
{
    /// <summary>
    /// Diet plans for assigned clients (UC21). Only the author edits a plan, and only while the client is assigned
    /// (BR-08); anything else is 404.
    /// </summary>
    public class DietPlansController : TrainerAreaController
    {
        private readonly IDietService _diet;

        /// <summary>
        /// Creates the controller.
        /// </summary>
        /// <param name="diet">The diet service.</param>
        public DietPlansController(IDietService diet)
        {
            _diet = diet;
        }

        /// <summary>
        /// The trainer's diet plans with their status, 20 per page.
        /// </summary>
        /// <param name="page">The page number.</param>
        /// <returns>The list.</returns>
        [HttpGet]
        public async Task<IActionResult> Index(int page = 1) => View(await _diet.GetTrainerPlansAsync(CurrentUserId, page));

        /// <summary>
        /// The new plan form; without a client it asks for one first, so the calorie target can be suggested.
        /// </summary>
        /// <param name="clientId">The chosen client.</param>
        /// <returns>The form, or 404 for a client who isn't assigned to the trainer.</returns>
        [HttpGet]
        public async Task<IActionResult> Create(int? clientId)
        {
            var model = await _diet.GetCreatePlanFormAsync(CurrentUserId, clientId);
            return model is null ? NotFound() : View(model);
        }

        /// <summary>
        /// Creates the Draft plan, then opens it to add meals.
        /// </summary>
        /// <param name="model">The form.</param>
        /// <returns>The editor, or the form with its errors.</returns>
        [HttpPost]
        public async Task<IActionResult> Create(DietPlanViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var planId = await _diet.CreatePlanAsync(CurrentUserId, model);
                    if (planId is null)
                    {
                        return NotFound();
                    }

                    SetSuccess("Diet plan created. Now add its meals.");
                    return RedirectToAction(nameof(Edit), new { id = planId });
                }
                catch (BusinessRuleException ex)
                {
                    ModelState.AddModelError(string.Empty, ex.Message);
                }
            }

            await _diet.FillPlanOptionsAsync(model, CurrentUserId);
            return View(model);
        }

        /// <summary>
        /// The plan editor: details, the meals and the add-meal form.
        /// </summary>
        /// <param name="id">The plan.</param>
        /// <returns>The editor, or 404.</returns>
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var model = await _diet.GetPlanForEditAsync(id, CurrentUserId);
            return model is null ? NotFound() : View(model);
        }

        /// <summary>
        /// Saves the details and meal changes, removing the ticked meals.
        /// </summary>
        /// <param name="id">The plan.</param>
        /// <param name="model">The editor.</param>
        /// <returns>The editor again.</returns>
        [HttpPost]
        public async Task<IActionResult> Edit(int id, DietPlanEditViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    if (!await _diet.UpdatePlanAsync(id, CurrentUserId, model))
                    {
                        return NotFound();
                    }

                    SetSuccess("The plan has been saved.");
                    return RedirectToAction(nameof(Edit), new { id });
                }
                catch (BusinessRuleException ex)
                {
                    ModelState.AddModelError(string.Empty, ex.Message);
                }
            }

            return await _diet.FillEditDisplayAsync(model, id, CurrentUserId) ? View(model) : NotFound();
        }

        /// <summary>
        /// Adds a scheduled meal to the plan.
        /// </summary>
        /// <param name="id">The plan.</param>
        /// <param name="model">The new meal.</param>
        /// <returns>The editor.</returns>
        [HttpPost]
        public async Task<IActionResult> AddMeal(int id, PlanMealViewModel model)
        {
            if (!ModelState.IsValid)
            {
                SetError(string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).Distinct()));
                return RedirectToAction(nameof(Edit), new { id });
            }

            try
            {
                if (!await _diet.AddMealAsync(id, CurrentUserId, model))
                {
                    return NotFound();
                }

                SetSuccess($"{model.MealType!.Value.GetDisplayName()} added for {(model.DayOfWeek is { } day ? day.ToString() : "every day")}.");
            }
            catch (BusinessRuleException ex)
            {
                SetError(ex.Message);
            }

            return RedirectToAction(nameof(Edit), new { id });
        }

        /// <summary>
        /// Activates a draft plan; the client's previous diet plan is completed (BR-09) and the client is notified.
        /// </summary>
        /// <param name="id">The plan.</param>
        /// <returns>The editor.</returns>
        [HttpPost]
        public async Task<IActionResult> Activate(int id)
        {
            try
            {
                if (!await _diet.ActivatePlanAsync(id, CurrentUserId))
                {
                    return NotFound();
                }

                SetSuccess("The diet plan is now active. The client has been notified.");
            }
            catch (BusinessRuleException ex)
            {
                SetError(ex.Message);
            }

            return RedirectToAction(nameof(Edit), new { id });
        }
    }
}
