using HealthTrack.Data;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Services
{
    /// <summary>
    /// When clients last did anything and how their weight has changed, shared by the trainer's client list, the dashboards
    /// and the reports.
    /// </summary>
    internal static class ClientActivity
    {
        /// <summary>Clients with no activity for this many days are flagged on the trainer's dashboard (spec 1.4).</summary>
        public const int InactiveAfterDays = 7;

        /// <summary>
        /// The latest day each client logged a workout or a meal or recorded a measurement.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="clientIds">The clients.</param>
        /// <returns>The last active day per client; clients who never logged anything are left out.</returns>
        public static async Task<Dictionary<int, DateTime>> LastActiveDaysAsync(ApplicationDbContext db, IReadOnlyCollection<int> clientIds)
        {
            var ids = clientIds.Distinct().ToList();
            var workouts = await db.WorkoutLogs.AsNoTracking()
                .Where(l => ids.Contains(l.ClientId))
                .GroupBy(l => l.ClientId)
                .Select(g => new { ClientId = g.Key, Last = g.Max(l => l.WorkoutDate) })
                .ToListAsync();
            var meals = await db.MealLogs.AsNoTracking()
                .Where(l => ids.Contains(l.ClientId))
                .GroupBy(l => l.ClientId)
                .Select(g => new { ClientId = g.Key, Last = g.Max(l => l.MealDate) })
                .ToListAsync();
            var measurements = await db.HealthMeasurements.AsNoTracking()
                .Where(m => ids.Contains(m.ClientId))
                .GroupBy(m => m.ClientId)
                .Select(g => new { ClientId = g.Key, Last = g.Max(m => m.MeasuredAt) })
                .ToListAsync();

            return workouts.Concat(meals).Concat(measurements)
                .GroupBy(a => a.ClientId)
                .ToDictionary(g => g.Key, g => g.Max(a => a.Last).Date);
        }

        /// <summary>
        /// Each client's weight change from the first measurement to the latest.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="clientIds">The clients.</param>
        /// <returns>The change in kg per client (negative is a loss); clients with fewer than two measurements are left out.</returns>
        public static async Task<Dictionary<int, decimal>> WeightChangesAsync(ApplicationDbContext db, IReadOnlyCollection<int> clientIds)
        {
            var ids = clientIds.Distinct().ToList();
            var readings = await db.HealthMeasurements.AsNoTracking()
                .Where(m => ids.Contains(m.ClientId))
                .Select(m => new { m.ClientId, m.MeasuredAt, m.MeasurementId, m.WeightKg })
                .ToListAsync();

            return readings
                .GroupBy(m => m.ClientId)
                .Where(g => g.Count() > 1)
                .ToDictionary(g => g.Key, g =>
                {
                    var ordered = g.OrderBy(m => m.MeasuredAt).ThenBy(m => m.MeasurementId).ToList();
                    return ordered[^1].WeightKg - ordered[0].WeightKg;
                });
        }
    }
}
