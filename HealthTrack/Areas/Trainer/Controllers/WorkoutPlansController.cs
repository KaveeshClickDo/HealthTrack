using HealthTrack.Helpers;
using HealthTrack.Models.ViewModels;
using HealthTrack.Services;
using Microsoft.AspNetCore.Mvc;

namespace HealthTrack.Areas.Trainer.Controllers
{
    /// <summary>
    /// Workout plans and routine templates (UC19, UC20, UC39). Only the author edits a plan, and client plans only
    /// while the client is assigned (BR-08); anything else is 404.
    /// </summary>
    public class WorkoutPlansController : TrainerAreaController
    {
        private readonly IWorkoutService _workouts;

        /// <summary>
        /// Creates the controller.
        /// </summary>
        /// <param name="workouts">The workout service.</param>
        public WorkoutPlansController(IWorkoutService workouts)
        {
            _workouts = workouts;
        }

        /// <summary>
        /// The trainer's client plans with their status, 20 per page.
        /// </summary>
        /// <param name="page">The page number.</param>
        /// <returns>The list.</returns>
        [HttpGet]
        public async Task<IActionResult> Index(int page = 1) => View(await _workouts.GetTrainerPlansAsync(CurrentUserId, page));

        /// <summary>
        /// The new plan form: a client plan, or a template when <paramref name="template"/> is true.
        /// </summary>
        /// <param name="clientId">The client to pre-select.</param>
        /// <param name="template">Whether to create a template.</param>
        /// <returns>The form, or 404 for a client who isn't assigned to the trainer.</returns>
        [HttpGet]
        public async Task<IActionResult> Create(int? clientId, bool template = false)
        {
            var model = await _workouts.GetCreatePlanFormAsync(CurrentUserId, clientId, template);
            return model is null ? NotFound() : View(model);
        }

        /// <summary>
        /// Creates the Draft plan, then opens it to add exercises.
        /// </summary>
        /// <param name="model">The form.</param>
        /// <returns>The editor, or the form with its errors.</returns>
        [HttpPost]
        public async Task<IActionResult> Create(WorkoutPlanViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var planId = await _workouts.CreatePlanAsync(CurrentUserId, model);
                    if (planId is null)
                    {
                        return NotFound();
                    }

                    SetSuccess(model.IsTemplate ? "Template created. Now add its exercises." : "Plan created. Now add its exercises.");
                    return RedirectToAction(nameof(Edit), new { id = planId });
                }
                catch (BusinessRuleException ex)
                {
                    ModelState.AddModelError(string.Empty, ex.Message);
                }
            }

            await _workouts.FillPlanOptionsAsync(model, CurrentUserId);
            return View(model);
        }

        /// <summary>
        /// The plan editor: details, the lines by weekday and the add-exercise form.
        /// </summary>
        /// <param name="id">The plan.</param>
        /// <returns>The editor, or 404.</returns>
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var model = await _workouts.GetPlanForEditAsync(id, CurrentUserId);
            return model is null ? NotFound() : View(model);
        }

        /// <summary>
        /// Saves the details and line changes, removing the ticked lines.
        /// </summary>
        /// <param name="id">The plan.</param>
        /// <param name="model">The editor.</param>
        /// <returns>The editor again.</returns>
        [HttpPost]
        public async Task<IActionResult> Edit(int id, WorkoutPlanEditViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    if (!await _workouts.UpdatePlanAsync(id, CurrentUserId, model))
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

            return await _workouts.FillEditDisplayAsync(model, id, CurrentUserId) ? View(model) : NotFound();
        }

        /// <summary>
        /// Adds an exercise to the plan on one weekday.
        /// </summary>
        /// <param name="id">The plan.</param>
        /// <param name="model">The new line.</param>
        /// <returns>The editor.</returns>
        [HttpPost]
        public async Task<IActionResult> AddExercise(int id, PlanExerciseViewModel model)
        {
            if (!ModelState.IsValid)
            {
                SetError(string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).Distinct()));
                return RedirectToAction(nameof(Edit), new { id });
            }

            try
            {
                if (!await _workouts.AddExerciseAsync(id, CurrentUserId, model))
                {
                    return NotFound();
                }

                SetSuccess($"Exercise added to {model.DayOfWeek}.");
            }
            catch (BusinessRuleException ex)
            {
                SetError(ex.Message);
            }

            return RedirectToAction(nameof(Edit), new { id });
        }

        /// <summary>
        /// Saves a client plan as a reusable template (UC39); the client plan is unchanged.
        /// </summary>
        /// <param name="id">The client plan.</param>
        /// <returns>The new template's editor.</returns>
        [HttpPost]
        public async Task<IActionResult> SaveAsTemplate(int id)
        {
            try
            {
                var templateId = await _workouts.SaveAsTemplateAsync(id, CurrentUserId);
                if (templateId is null)
                {
                    return NotFound();
                }

                SetSuccess("Saved as a template. Any trainer can now assign it to their clients.");
                return RedirectToAction(nameof(Edit), new { id = templateId });
            }
            catch (BusinessRuleException ex)
            {
                SetError(ex.Message);
                return RedirectToAction(nameof(Edit), new { id });
            }
        }

        /// <summary>
        /// Every routine template, with its author, size and how often it was assigned.
        /// </summary>
        /// <param name="clientId">A client to carry into the assign links.</param>
        /// <returns>The list.</returns>
        [HttpGet]
        public async Task<IActionResult> Templates(int? clientId)
        {
            ViewData["ClientId"] = clientId;
            return View(await _workouts.GetTemplatesAsync(CurrentUserId));
        }

        /// <summary>
        /// The form for assigning a template to one of the trainer's clients.
        /// </summary>
        /// <param name="id">The template.</param>
        /// <param name="clientId">The client to pre-select.</param>
        /// <returns>The form, or 404.</returns>
        [HttpGet]
        public async Task<IActionResult> AssignTemplate(int id, int? clientId)
        {
            var model = await _workouts.GetAssignTemplateFormAsync(id, CurrentUserId, clientId);
            return model is null ? NotFound() : View(model);
        }

        /// <summary>
        /// Copies the template to the client (BR-12), optionally activating it.
        /// </summary>
        /// <param name="id">The template.</param>
        /// <param name="model">The form.</param>
        /// <returns>The new plan's editor, or the form with its errors.</returns>
        [HttpPost]
        public async Task<IActionResult> AssignTemplate(int id, AssignTemplateViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var planId = await _workouts.AssignTemplateAsync(id, model.ClientId!.Value, model.StartDate!.Value,
                        model.ActivateNow, CurrentUserId);
                    if (planId is null)
                    {
                        return NotFound();
                    }

                    SetSuccess(model.ActivateNow
                        ? "The template has been copied to the client and activated. They have been notified."
                        : "The template has been copied to the client as a draft. Activate it when it's ready.");
                    return RedirectToAction(nameof(Edit), new { id = planId });
                }
                catch (BusinessRuleException ex)
                {
                    ModelState.AddModelError(string.Empty, ex.Message);
                }
            }

            return await _workouts.FillAssignTemplateDisplayAsync(model, id, CurrentUserId) ? View(model) : NotFound();
        }

        /// <summary>
        /// Activates a draft client plan; the client's previous plan is completed (BR-09) and the client is notified.
        /// </summary>
        /// <param name="id">The plan.</param>
        /// <returns>The editor.</returns>
        [HttpPost]
        public async Task<IActionResult> Activate(int id)
        {
            try
            {
                if (!await _workouts.ActivatePlanAsync(id, CurrentUserId))
                {
                    return NotFound();
                }

                SetSuccess("The plan is now active. The client has been notified.");
            }
            catch (BusinessRuleException ex)
            {
                SetError(ex.Message);
            }

            return RedirectToAction(nameof(Edit), new { id });
        }

        /// <summary>
        /// Archives a plan or template; plans are archived, never deleted (BR-21).
        /// </summary>
        /// <param name="id">The plan.</param>
        /// <returns>The editor, now read-only.</returns>
        [HttpPost]
        public async Task<IActionResult> Archive(int id)
        {
            try
            {
                if (!await _workouts.ArchivePlanAsync(id, CurrentUserId))
                {
                    return NotFound();
                }

                SetSuccess("The plan has been archived. It stays in the history.");
            }
            catch (BusinessRuleException ex)
            {
                SetError(ex.Message);
            }

            return RedirectToAction(nameof(Edit), new { id });
        }
    }
}
