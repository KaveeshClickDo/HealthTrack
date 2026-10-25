using HealthTrack.Helpers;
using HealthTrack.Models.ViewModels;
using HealthTrack.Services;
using Microsoft.AspNetCore.Mvc;

namespace HealthTrack.Areas.Client.Controllers
{
    /// <summary>
    /// The client's health measurements (UC11, UC37).
    /// </summary>
    public class MeasurementsController : ClientAreaController
    {
        private readonly IProgressService _progress;

        /// <summary>
        /// Creates the controller.
        /// </summary>
        /// <param name="progress">The progress service.</param>
        public MeasurementsController(IProgressService progress)
        {
            _progress = progress;
        }

        /// <summary>
        /// The latest reading with its BMI category, and the readings, 20 per page.
        /// </summary>
        /// <param name="page">The page number.</param>
        /// <returns>The page.</returns>
        [HttpGet]
        public async Task<IActionResult> Index(int page = 1)
        {
            if (await CurrentClientIdAsync() is not { } clientId)
            {
                return NotFound();
            }

            return View(await _progress.GetMeasurementsAsync(clientId, page));
        }

        /// <summary>
        /// The new measurement form, with the height from the last reading.
        /// </summary>
        /// <returns>The form.</returns>
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            if (await CurrentClientIdAsync() is not { } clientId)
            {
                return NotFound();
            }

            return View(await _progress.GetMeasurementFormAsync(clientId));
        }

        /// <summary>
        /// Saves the measurement; BMI is calculated.
        /// </summary>
        /// <param name="model">The form.</param>
        /// <returns>The measurements page, or the form with its errors.</returns>
        [HttpPost]
        public async Task<IActionResult> Create(MeasurementViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    if (await _progress.AddMeasurementAsync(CurrentUserId, model) is null)
                    {
                        return NotFound();
                    }

                    SetSuccess("Your measurement has been saved.");
                    return RedirectToAction(nameof(Index));
                }
                catch (BusinessRuleException ex)
                {
                    ModelState.AddModelError(string.Empty, ex.Message);
                }
            }

            return View(model);
        }
    }
}
