using HealthTrack.Helpers;
using HealthTrack.Models.ViewModels;
using HealthTrack.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Areas.Trainer.Controllers
{
    /// <summary>
    /// The professional's appointments (UC24–UC26, UC14): decide on requests, reschedule, cancel and record the outcome.
    /// Only the appointment's own professional reaches it (BR-08); anything else is 404.
    /// </summary>
    public class AppointmentsController : TrainerAreaController
    {
        private readonly IAppointmentService _appointments;

        /// <summary>
        /// Creates the controller.
        /// </summary>
        /// <param name="appointments">The appointment service.</param>
        public AppointmentsController(IAppointmentService appointments)
        {
            _appointments = appointments;
        }

        /// <summary>
        /// Pending requests first, then today and upcoming, then past (20 per page).
        /// </summary>
        /// <param name="page">The page of the past list.</param>
        /// <returns>The list.</returns>
        [HttpGet]
        public async Task<IActionResult> Index(int page = 1) => View(await _appointments.GetForTrainerAsync(CurrentUserId, page));

        /// <summary>
        /// One appointment with the client's reason, the notes and the actions available now.
        /// </summary>
        /// <param name="id">The appointment.</param>
        /// <returns>The page, or 404.</returns>
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var model = await _appointments.GetDetailsAsync(id, CurrentUserId);
            return model is null || !model.ViewerIsTrainer ? NotFound() : View(model);
        }

        /// <summary>
        /// Approves a pending request, optionally with a room or meeting link.
        /// </summary>
        /// <param name="id">The appointment.</param>
        /// <param name="model">The action form.</param>
        /// <returns>The appointment.</returns>
        [HttpPost]
        public Task<IActionResult> Approve(int id, AppointmentActionViewModel model) =>
            RunAsync(id, model, () => _appointments.ApproveAsync(id, CurrentUserId, model.Location, model.RowVersion),
                "The appointment is approved. The client has been notified.");

        /// <summary>
        /// Rejects a pending request; a reason is required (BR-16).
        /// </summary>
        /// <param name="id">The appointment.</param>
        /// <param name="model">The action form.</param>
        /// <returns>The appointment.</returns>
        [HttpPost]
        public Task<IActionResult> Reject(int id, AppointmentActionViewModel model) =>
            RunAsync(id, model, () => _appointments.RejectAsync(id, CurrentUserId, model.Reason, model.RowVersion),
                "The request has been declined. The client has been notified.");

        /// <summary>
        /// The reschedule form: a date picker and that day's free times; changing the date or length reloads it.
        /// </summary>
        /// <param name="id">The appointment.</param>
        /// <param name="date">The day to show.</param>
        /// <param name="minutes">The length.</param>
        /// <returns>The form, or 404.</returns>
        [HttpGet]
        public async Task<IActionResult> Reschedule(int id, DateTime? date, int? minutes)
        {
            var model = await _appointments.GetRescheduleFormAsync(id, CurrentUserId, date, minutes);
            return model is null ? NotFound() : View(model);
        }

        /// <summary>
        /// Moves the appointment to the chosen free time (BR-15, UC25).
        /// </summary>
        /// <param name="id">The appointment.</param>
        /// <param name="model">The form.</param>
        /// <returns>The appointment, or the form with its errors.</returns>
        [HttpPost]
        public async Task<IActionResult> Reschedule(int id, RescheduleViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    if (!await _appointments.RescheduleAsync(id, CurrentUserId, model.NewStart!.Value, model.DurationMinutes!.Value, model.RowVersion))
                    {
                        return NotFound();
                    }

                    SetSuccess("The appointment has been moved. The client has been notified.");
                    return RedirectToAction(nameof(Details), new { id });
                }
                catch (BusinessRuleException ex)
                {
                    ModelState.AddModelError(string.Empty, ex.Message);
                }
                catch (DbUpdateConcurrencyException)
                {
                    SetError(AppointmentService.ConcurrencyMessage);
                    return RedirectToAction(nameof(Details), new { id });
                }
            }

            return await _appointments.FillRescheduleDisplayAsync(model, id, CurrentUserId) ? View(model) : NotFound();
        }

        /// <summary>
        /// Cancels a confirmed appointment before its start; a reason is required (BR-16, BR-17).
        /// </summary>
        /// <param name="id">The appointment.</param>
        /// <param name="model">The action form.</param>
        /// <returns>The appointment.</returns>
        [HttpPost]
        public Task<IActionResult> Cancel(int id, AppointmentActionViewModel model) =>
            RunAsync(id, model, () => _appointments.CancelAsync(id, CurrentUserId, model.Reason, model.RowVersion),
                "The appointment has been cancelled. The client has been notified.");

        /// <summary>
        /// Records the appointment as held, after its start, with consultation notes (stored encrypted).
        /// </summary>
        /// <param name="id">The appointment.</param>
        /// <param name="model">The action form.</param>
        /// <returns>The appointment.</returns>
        [HttpPost]
        public Task<IActionResult> Complete(int id, AppointmentActionViewModel model) =>
            RunAsync(id, model, () => _appointments.CompleteAsync(id, CurrentUserId, model.Notes, model.RowVersion),
                "The appointment is marked as completed.");

        /// <summary>
        /// Records that the client didn't attend, after the start.
        /// </summary>
        /// <param name="id">The appointment.</param>
        /// <param name="model">The action form.</param>
        /// <returns>The appointment.</returns>
        [HttpPost]
        public Task<IActionResult> NoShow(int id, AppointmentActionViewModel model) =>
            RunAsync(id, model, () => _appointments.MarkNoShowAsync(id, CurrentUserId, model.RowVersion),
                "The appointment is recorded as a no-show.");

        // Runs one lifecycle action and shows its outcome on the appointment's page. A broken rule or a clashing change
        // becomes the error message; an appointment that isn't the trainer's is 404.
        private async Task<IActionResult> RunAsync(int id, AppointmentActionViewModel model, Func<Task<bool>> action, string success)
        {
            if (!ModelState.IsValid)
            {
                SetError(string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).Distinct()));
                return RedirectToAction(nameof(Details), new { id });
            }

            try
            {
                if (!await action())
                {
                    return NotFound();
                }

                SetSuccess(success);
            }
            catch (BusinessRuleException ex)
            {
                SetError(ex.Message);
            }
            catch (DbUpdateConcurrencyException)
            {
                SetError(AppointmentService.ConcurrencyMessage);
            }

            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
