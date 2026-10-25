using HealthTrack.Helpers;
using HealthTrack.Models.ViewModels;
using HealthTrack.Services;
using Microsoft.AspNetCore.Mvc;

namespace HealthTrack.Areas.Client.Controllers
{
    /// <summary>
    /// The client's progress charts (UC12). The page loads its chart data from the JSON actions, so changing the
    /// range only re-fetches the data.
    /// </summary>
    public class ProgressController : ClientAreaController
    {
        private readonly IProgressService _progress;
        private readonly IClock _clock;

        /// <summary>
        /// Creates the controller.
        /// </summary>
        /// <param name="progress">The progress service.</param>
        /// <param name="clock">Supplies today's date for the range.</param>
        public ProgressController(IProgressService progress, IClock clock)
        {
            _progress = progress;
            _clock = clock;
        }

        /// <summary>
        /// The progress page with its range picker.
        /// </summary>
        /// <param name="range">4w, 3m or custom.</param>
        /// <param name="from">The custom start.</param>
        /// <param name="to">The custom end.</param>
        /// <returns>The page.</returns>
        [HttpGet]
        public async Task<IActionResult> Index(string? range, DateTime? from, DateTime? to)
        {
            if (await CurrentClientIdAsync() is not { } clientId)
            {
                return NotFound();
            }

            return View(await _progress.GetClientProgressAsync(clientId, Resolve(range, from, to)));
        }

        /// <summary>
        /// Weight with the target weight, and BMI, as JSON.
        /// </summary>
        /// <param name="range">4w, 3m or custom.</param>
        /// <param name="from">The custom start.</param>
        /// <param name="to">The custom end.</param>
        /// <returns>{ period, weight, bmi }.</returns>
        [HttpGet]
        public async Task<IActionResult> WeightData(string? range, DateTime? from, DateTime? to)
        {
            if (await CurrentClientIdAsync() is not { } clientId)
            {
                return NotFound();
            }

            var dates = Resolve(range, from, to);
            return Json(new
            {
                period = Period(dates),
                weight = await _progress.GetWeightTrendAsync(clientId, dates.From, dates.To),
                bmi = await _progress.GetBmiTrendAsync(clientId, dates.From, dates.To)
            });
        }

        /// <summary>
        /// Weekly kcal burned and eaten against the diet target, as JSON.
        /// </summary>
        /// <param name="range">4w, 3m or custom.</param>
        /// <param name="from">The custom start.</param>
        /// <param name="to">The custom end.</param>
        /// <returns>{ period, calories }.</returns>
        [HttpGet]
        public async Task<IActionResult> CaloriesData(string? range, DateTime? from, DateTime? to)
        {
            if (await CurrentClientIdAsync() is not { } clientId)
            {
                return NotFound();
            }

            var dates = Resolve(range, from, to);
            return Json(new { period = Period(dates), calories = await _progress.GetCaloriesTrendAsync(clientId, dates.From, dates.To) });
        }

        /// <summary>
        /// Resting heart rate and blood pressure, as JSON.
        /// </summary>
        /// <param name="range">4w, 3m or custom.</param>
        /// <param name="from">The custom start.</param>
        /// <param name="to">The custom end.</param>
        /// <returns>{ period, vitals }.</returns>
        [HttpGet]
        public async Task<IActionResult> VitalsData(string? range, DateTime? from, DateTime? to)
        {
            if (await CurrentClientIdAsync() is not { } clientId)
            {
                return NotFound();
            }

            var dates = Resolve(range, from, to);
            return Json(new { period = Period(dates), vitals = await _progress.GetVitalsTrendAsync(clientId, dates.From, dates.To) });
        }

        private ProgressRangeViewModel Resolve(string? range, DateTime? from, DateTime? to) =>
            ProgressRangeViewModel.Resolve(range, from, to, _clock.Today);

        // The dates the data covers, shown next to the range picker after a re-fetch.
        private static string Period(ProgressRangeViewModel range) => $"{range.From:dd MMM yyyy} – {range.To:dd MMM yyyy}";
    }
}
