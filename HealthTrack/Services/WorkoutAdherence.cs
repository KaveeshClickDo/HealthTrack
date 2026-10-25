using HealthTrack.Data;
using HealthTrack.Models.Enums;
using HealthTrack.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Services
{
    /// <summary>
    /// Workout adherence (spec 1.10) for one client or many at once, shared by ProgressService, the dashboards and the
    /// reports, so every page counts it the same way: the scheduled days of the plan in effect, up to today, and how many
    /// of them have a logged session.
    /// </summary>
    internal static class WorkoutAdherence
    {
        /// <summary>
        /// Every day in the range on which a client's plan in effect scheduled training. The plan in effect is the active or
        /// completed plan with the latest start date on or before the day (BR-09); days after today aren't counted yet.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="clientIds">The clients.</param>
        /// <param name="from">The first day.</param>
        /// <param name="to">The last day.</param>
        /// <param name="today">Today's date.</param>
        /// <returns>One row per scheduled day, with the plan in effect and whether a session was logged.</returns>
        public static async Task<List<ScheduledWorkoutDay>> ScheduledDaysAsync(ApplicationDbContext db, IReadOnlyCollection<int> clientIds,
            DateTime from, DateTime to, DateTime today)
        {
            var first = from.Date;
            var last = to.Date > today.Date ? today.Date : to.Date;
            var ids = clientIds.Distinct().ToList();
            if (ids.Count == 0 || last < first)
            {
                return [];
            }

            var plans = await db.WorkoutPlans.AsNoTracking()
                .Where(p => p.ClientId != null && ids.Contains(p.ClientId.Value) && !p.IsTemplate && p.StartDate != null
                    && (p.Status == PlanStatus.Active || p.Status == PlanStatus.Completed))
                .Select(p => new
                {
                    ClientId = p.ClientId!.Value,
                    p.WorkoutPlanId,
                    StartDate = p.StartDate!.Value,
                    p.EndDate,
                    Days = p.Exercises.Select(e => e.DayOfWeek).Distinct().ToList()
                })
                .ToListAsync();
            var logged = (await db.WorkoutLogs.AsNoTracking()
                    .Where(l => ids.Contains(l.ClientId) && l.WorkoutDate >= first && l.WorkoutDate <= last)
                    .Select(l => new { l.ClientId, l.WorkoutDate })
                    .ToListAsync())
                .Select(l => (l.ClientId, l.WorkoutDate.Date))
                .ToHashSet();

            var days = new List<ScheduledWorkoutDay>();
            foreach (var clientPlans in plans.GroupBy(p => p.ClientId))
            {
                for (var day = first; day <= last; day = day.AddDays(1))
                {
                    var plan = clientPlans
                        .Where(p => p.StartDate <= day && (p.EndDate == null || p.EndDate >= day))
                        .OrderByDescending(p => p.StartDate)
                        .ThenByDescending(p => p.WorkoutPlanId)
                        .FirstOrDefault();
                    if (plan is not null && plan.Days.Contains(day.DayOfWeek))
                    {
                        days.Add(new ScheduledWorkoutDay(clientPlans.Key, plan.WorkoutPlanId, day, logged.Contains((clientPlans.Key, day))));
                    }
                }
            }

            return days;
        }

        /// <summary>
        /// Adherence per client over the range; a client with nothing scheduled gets an empty figure (no percentage).
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="clientIds">The clients.</param>
        /// <param name="from">The first day.</param>
        /// <param name="to">The last day.</param>
        /// <param name="today">Today's date.</param>
        /// <returns>One entry for every client asked about.</returns>
        public static async Task<Dictionary<int, AdherenceViewModel>> ForClientsAsync(ApplicationDbContext db, IReadOnlyCollection<int> clientIds,
            DateTime from, DateTime to, DateTime today)
        {
            var days = await ScheduledDaysAsync(db, clientIds, from, to, today);
            return clientIds.Distinct().ToDictionary(id => id, id => Summarise(days.Where(d => d.ClientId == id)));
        }

        /// <summary>
        /// Adds up scheduled days into an adherence figure.
        /// </summary>
        /// <param name="days">The scheduled days to count.</param>
        /// <returns>The figure.</returns>
        public static AdherenceViewModel Summarise(IEnumerable<ScheduledWorkoutDay> days)
        {
            var list = days.ToList();
            return new AdherenceViewModel { ScheduledDays = list.Count, CompletedDays = list.Count(d => d.Logged) };
        }
    }
}
