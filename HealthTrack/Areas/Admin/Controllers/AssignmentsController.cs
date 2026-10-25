using HealthTrack.Helpers;
using HealthTrack.Models.ViewModels;
using HealthTrack.Services;
using Microsoft.AspNetCore.Mvc;

namespace HealthTrack.Areas.Admin.Controllers
{
    /// <summary>
    /// Assign trainers to clients and end assignments (UC31, BR-06, BR-07).
    /// </summary>
    public class AssignmentsController : AdminAreaController
    {
        private readonly IAssignmentService _assignments;

        /// <summary>
        /// Creates the controller.
        /// </summary>
        /// <param name="assignments">The assignment service.</param>
        public AssignmentsController(IAssignmentService assignments)
        {
            _assignments = assignments;
        }

        /// <summary>
        /// Assignments (active first), unassigned clients and each trainer's load against capacity.
        /// </summary>
        /// <param name="filter">The filters from the query string.</param>
        /// <param name="page">The page of assignments.</param>
        /// <returns>The page.</returns>
        [HttpGet]
        public async Task<IActionResult> Index(AssignmentFilterViewModel filter, int page = 1) =>
            View(await _assignments.GetOverviewAsync(filter, page));

        /// <summary>
        /// The assignment form, optionally for one client.
        /// </summary>
        /// <param name="clientId">The client to pre-select.</param>
        /// <returns>The form.</returns>
        [HttpGet]
        public async Task<IActionResult> Assign(int? clientId) => View(await _assignments.GetAssignFormAsync(clientId));

        /// <summary>
        /// Assigns the trainer; the client's current assignment ends the same day.
        /// </summary>
        /// <param name="model">The form.</param>
        /// <returns>The assignments page, or the form with its errors.</returns>
        [HttpPost]
        public async Task<IActionResult> Assign(AssignTrainerViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    if (!await _assignments.AssignTrainerAsync(model.ClientId!.Value, model.TrainerId!.Value,
                            model.StartDate!.Value, model.Notes, CurrentUserId))
                    {
                        return NotFound();
                    }

                    SetSuccess("The trainer has been assigned. Both of them have been notified.");
                    return RedirectToAction(nameof(Index));
                }
                catch (BusinessRuleException ex)
                {
                    ModelState.AddModelError(string.Empty, ex.Message);
                }
            }

            await _assignments.FillAssignOptionsAsync(model);
            return View(model);
        }

        /// <summary>
        /// Ends an assignment; it stays in the list as history.
        /// </summary>
        /// <param name="id">The assignment's key.</param>
        /// <param name="endDate">The last day.</param>
        /// <returns>The assignments page.</returns>
        [HttpPost]
        public async Task<IActionResult> End(int id, DateTime endDate)
        {
            try
            {
                if (!await _assignments.EndAssignmentAsync(id, endDate, CurrentUserId))
                {
                    return NotFound();
                }

                SetSuccess("The assignment has been ended.");
            }
            catch (BusinessRuleException ex)
            {
                SetError(ex.Message);
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
