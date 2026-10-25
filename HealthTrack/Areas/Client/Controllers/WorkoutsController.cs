using HealthTrack.Helpers;
using HealthTrack.Models.ViewModels;
using HealthTrack.Services;
using Microsoft.AspNetCore.Mvc;

namespace HealthTrack.Areas.Client.Controllers
{
    /// <summary>
    /// The client's workouts: this week's plan, today's session, free logging and history (UC08, UC09).
    /// </summary>
    public class WorkoutsController : ClientAreaController
    {
        private readonly IWorkoutService _workouts;
        private readonly IClock _clock;

        /// <summary>
        /// Creates the controller.
        /// </summary>
        /// <param name="workouts">The workout service.</param>
        /// <param name="clock">Supplies today's date for today's workout.</param>
        public WorkoutsController(IWorkoutService workouts, IClock clock)
        {
            _workouts = workouts;
            _clock = clock;
        }

        /// <summary>
        /// The active plan as a Monday–Sunday grid.
        /// </summary>
        /// <returns>The week.</returns>
        [HttpGet]
        public async Task<IActionResult> MyPlan()
        {
            if (await CurrentClientIdAsync() is not { } clientId)
            {
                return NotFound();
            }

            return View(await _workouts.GetWeekAsync(clientId));
        }

        /// <summary>
        /// Today's planned exercises, pre-filled to tick off and adjust.
        /// </summary>
        /// <returns>Today's log form.</returns>
        [HttpGet]
        public async Task<IActionResult> Today()
        {
            if (await CurrentClientIdAsync() is not { } clientId)
            {
                return NotFound();
            }

            return View(await _workouts.GetTodaysWorkoutAsync(clientId, _clock.Today));
        }

        /// <summary>
        /// The free workout form.
        /// </summary>
        /// <returns>The form.</returns>
        [HttpGet]
        public async Task<IActionResult> Log() => View(await _workouts.GetLogFormAsync());

        /// <summary>
        /// Saves a session, from today's plan or a free workout.
        /// </summary>
        /// <param name="model">The form.</param>
        /// <returns>The history, or the form with its errors.</returns>
        [HttpPost]
        public async Task<IActionResult> Log(LogWorkoutViewModel model)
        {
            // A session from today's plan goes back to that page when something is wrong.
            var viewName = model.WorkoutPlanId is null ? nameof(Log) : nameof(Today);

            if (ModelState.IsValid)
            {
                try
                {
                    if (await _workouts.LogWorkoutAsync(CurrentUserId, model) is null)
                    {
                        return NotFound();
                    }

                    SetSuccess("Your workout has been saved. Calories were worked out from your latest weight.");
                    return RedirectToAction(nameof(History));
                }
                catch (BusinessRuleException ex)
                {
                    ModelState.AddModelError(string.Empty, ex.Message);
                }
            }

            model.HasActivePlan = model.WorkoutPlanId is not null;
            await _workouts.FillLogOptionsAsync(model);
            return View(viewName, model);
        }

        /// <summary>
        /// Past sessions with minutes, calories, entries and feedback, 20 per page.
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

            return View(await _workouts.GetHistoryAsync(clientId, page));
        }
    }
}
