using System.Globalization;
using HealthTrack.Helpers;
using HealthTrack.Models.ViewModels;
using HealthTrack.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Areas.Client.Controllers
{
    /// <summary>
    /// The client's appointments (UC13, UC14): booking a free slot, the list, the details and cancelling.
    /// </summary>
    public class AppointmentsController : ClientAreaController
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
        /// Upcoming appointments, then past ones (20 per page), with status badges.
        /// </summary>
        /// <param name="page">The page of the past list.</param>
        /// <returns>The list.</returns>
        [HttpGet]
        public async Task<IActionResult> Index(int page = 1)
        {
            if (await CurrentClientIdAsync() is not { } clientId)
            {
                return NotFound();
            }

            return View(await _appointments.GetForClientAsync(clientId, page));
        }

        /// <summary>
        /// One of the client's appointments.
        /// </summary>
        /// <param name="id">The appointment.</param>
        /// <returns>The page, or 404.</returns>
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var model = await _appointments.GetDetailsAsync(id, CurrentUserId);
            return model is null || model.ViewerIsTrainer ? NotFound() : View(model);
        }

        /// <summary>
        /// The booking form: a professional, then type, mode, date, length and one of the free times.
        /// </summary>
        /// <param name="trainerId">A professional to pre-select.</param>
        /// <returns>The form.</returns>
        [HttpGet]
        public async Task<IActionResult> Book(int? trainerId)
        {
            if (await CurrentClientIdAsync() is not { } clientId)
            {
                return NotFound();
            }

            return View(await _appointments.GetBookFormAsync(clientId, trainerId));
        }

        /// <summary>
        /// Books the appointment as a Pending request; the client is always the signed-in user (spec 3.2).
        /// </summary>
        /// <param name="model">The form.</param>
        /// <returns>The appointment list, or the form with its errors.</returns>
        [HttpPost]
        public async Task<IActionResult> Book(BookAppointmentViewModel model)
        {
            if (await CurrentClientIdAsync() is not { } clientId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    if (await _appointments.BookAsync(CurrentUserId, model) is null)
                    {
                        return NotFound();
                    }

                    SetSuccess("Your request has been sent. You'll get a notification when it is approved.");
                    return RedirectToAction(nameof(Index));
                }
                catch (BusinessRuleException ex)
                {
                    ModelState.AddModelError(string.Empty, ex.Message);
                }
            }

            await _appointments.FillBookOptionsAsync(model, clientId);
            return View(model);
        }

        /// <summary>
        /// The free start times for a professional, day and length, as JSON for wwwroot/js/appointment-book.js.
        /// </summary>
        /// <param name="trainerId">The professional.</param>
        /// <param name="date">The day.</param>
        /// <param name="minutes">The length.</param>
        /// <returns>{ slots: ["09:00", …] }.</returns>
        [HttpGet]
        public async Task<IActionResult> AvailableSlots(int trainerId, DateTime date, int minutes)
        {
            if (await CurrentClientIdAsync() is not { } clientId)
            {
                return NotFound();
            }

            var slots = await _appointments.GetAvailableSlotsAsync(trainerId, clientId, date, minutes);
            return Json(new { slots = slots.Select(s => s.ToString("HH:mm", CultureInfo.InvariantCulture)) });
        }

        /// <summary>
        /// Cancels the appointment; refused within 2 hours of the start (BR-17).
        /// </summary>
        /// <param name="id">The appointment.</param>
        /// <param name="model">The action form, with an optional reason.</param>
        /// <returns>The appointment list.</returns>
        [HttpPost]
        public async Task<IActionResult> Cancel(int id, AppointmentActionViewModel model)
        {
            if (!ModelState.IsValid)
            {
                SetError(string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).Distinct()));
                return RedirectToAction(nameof(Details), new { id });
            }

            try
            {
                if (!await _appointments.CancelAsync(id, CurrentUserId, model.Reason, model.RowVersion))
                {
                    return NotFound();
                }

                SetSuccess("The appointment has been cancelled. Your professional has been notified.");
                return RedirectToAction(nameof(Index));
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
