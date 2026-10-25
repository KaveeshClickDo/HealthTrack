using HealthTrack.Helpers;
using HealthTrack.Models.ViewModels;
using HealthTrack.Services;
using Microsoft.AspNetCore.Mvc;

namespace HealthTrack.Areas.Trainer.Controllers
{
    /// <summary>
    /// The trainer's weekly availability windows (UC23); clients can only book inside them (BR-15).
    /// </summary>
    public class AvailabilityController : TrainerAreaController
    {
        private readonly IAppointmentService _appointments;

        /// <summary>
        /// Creates the controller.
        /// </summary>
        /// <param name="appointments">The appointment service, which owns availability.</param>
        public AvailabilityController(IAppointmentService appointments)
        {
            _appointments = appointments;
        }

        /// <summary>
        /// The windows by weekday, with the add-window form.
        /// </summary>
        /// <returns>The page.</returns>
        [HttpGet]
        public async Task<IActionResult> Index() => View(await _appointments.GetAvailabilityAsync(CurrentUserId));

        /// <summary>
        /// Adds a window.
        /// </summary>
        /// <param name="model">The window.</param>
        /// <returns>The page, with the form's errors if it failed.</returns>
        [HttpPost]
        public async Task<IActionResult> Create([Bind(Prefix = nameof(AvailabilityPageViewModel.NewWindow))] AvailabilityViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    if (!await _appointments.AddAvailabilityAsync(CurrentUserId, model))
                    {
                        return NotFound();
                    }

                    SetSuccess($"Window added: {model.DayOfWeek} {model.StartTime:hh\\:mm}–{model.EndTime:hh\\:mm}.");
                    return RedirectToAction(nameof(Index));
                }
                catch (BusinessRuleException ex)
                {
                    ModelState.AddModelError(string.Empty, ex.Message);
                }
            }

            var page = await _appointments.GetAvailabilityAsync(CurrentUserId);
            page.NewWindow = model;
            return View(nameof(Index), page);
        }

        /// <summary>
        /// Removes a window; appointments already booked in it stay.
        /// </summary>
        /// <param name="id">The window.</param>
        /// <returns>The page.</returns>
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            if (!await _appointments.RemoveAvailabilityAsync(id, CurrentUserId))
            {
                return NotFound();
            }

            SetSuccess("The window has been removed. Appointments already booked in it are kept.");
            return RedirectToAction(nameof(Index));
        }
    }
}
