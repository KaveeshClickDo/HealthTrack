using HealthTrack.Services;
using Microsoft.AspNetCore.Mvc;

namespace HealthTrack.Areas.Trainer.Controllers
{
    /// <summary>
    /// The trainer's assigned clients (BR-08: only clients with an active assignment).
    /// </summary>
    public class ClientsController : TrainerAreaController
    {
        private readonly IAssignmentService _assignments;

        /// <summary>
        /// Creates the controller.
        /// </summary>
        /// <param name="assignments">The assignment service.</param>
        public ClientsController(IAssignmentService assignments)
        {
            _assignments = assignments;
        }

        /// <summary>
        /// The assigned clients with their goal, assignment date, last workout and active plan.
        /// </summary>
        /// <returns>The list.</returns>
        [HttpGet]
        public async Task<IActionResult> Index() => View(await _assignments.GetTrainerClientsAsync(CurrentUserId));
    }
}
