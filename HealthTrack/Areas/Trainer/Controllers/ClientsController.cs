using HealthTrack.Helpers;
using HealthTrack.Models.ViewModels;
using HealthTrack.Services;
using Microsoft.AspNetCore.Mvc;

namespace HealthTrack.Areas.Trainer.Controllers
{
    /// <summary>
    /// The trainer's assigned clients and their progress (BR-08: only clients with an active assignment).
    /// </summary>
    public class ClientsController : TrainerAreaController
    {
        private readonly IAssignmentService _assignments;
        private readonly IProgressService _progress;
        private readonly IClock _clock;

        /// <summary>
        /// Creates the controller.
        /// </summary>
        /// <param name="assignments">The assignment service.</param>
        /// <param name="progress">The progress service.</param>
        /// <param name="clock">Supplies today's date for the range.</param>
        public ClientsController(IAssignmentService assignments, IProgressService progress, IClock clock)
        {
            _assignments = assignments;
            _progress = progress;
            _clock = clock;
        }

        /// <summary>
        /// The assigned clients with their goal, assignment date, last workout and active plan.
        /// </summary>
        /// <returns>The list.</returns>
        [HttpGet]
        public async Task<IActionResult> Index() => View(await _assignments.GetTrainerClientsAsync(CurrentUserId));

        /// <summary>
        /// An assigned client's progress (UC18): charts, adherence, measurements and recent logs. Changing the range
        /// reloads the page with the charts' data embedded.
        /// </summary>
        /// <param name="clientId">The client.</param>
        /// <param name="range">4w, 3m or custom.</param>
        /// <param name="from">The custom start.</param>
        /// <param name="to">The custom end.</param>
        /// <returns>The page, or 404 for a client who isn't assigned to the trainer.</returns>
        [HttpGet]
        public async Task<IActionResult> Progress(int clientId, string? range, DateTime? from, DateTime? to)
        {
            var model = await _progress.GetTrainerClientProgressAsync(CurrentUserId, clientId,
                ProgressRangeViewModel.Resolve(range, from, to, _clock.Today));
            return model is null ? NotFound() : View(model);
        }
    }
}
