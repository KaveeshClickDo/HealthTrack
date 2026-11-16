using HealthTrack.Data;
using HealthTrack.Helpers;
using HealthTrack.Models.Enums;
using HealthTrack.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Services
{
    /// <summary>
    /// Sidebar counts, read with COUNT queries on every signed-in page (cheap: indexed columns only).
    /// </summary>
    public class NavigationService : INavigationService
    {
        private readonly ApplicationDbContext _db;
        private readonly IClock _clock;

        /// <summary>Creates the service.</summary>
        /// <param name="db">The database.</param>
        /// <param name="clock">The clock (upcoming requests only).</param>
        public NavigationService(ApplicationDbContext db, IClock clock)
        {
            _db = db;
            _clock = clock;
        }

        /// <inheritdoc />
        public async Task<NavCountsViewModel> GetCountsAsync(string userId, bool isTrainer, bool isAdmin)
        {
            var counts = new NavCountsViewModel();
            if (isTrainer)
            {
                var trainerId = await _db.TrainerProfiles.AsNoTracking().Where(t => t.UserId == userId).Select(t => (int?)t.TrainerId).FirstOrDefaultAsync();
                if (trainerId is not null)
                {
                    var now = _clock.Now;
                    counts.ActiveClients = await _db.TrainerAssignments.CountAsync(a => a.TrainerId == trainerId && a.EndDate == null);
                    counts.PendingRequests = await _db.Appointments.CountAsync(a => a.TrainerId == trainerId && a.Status == AppointmentStatus.Pending && a.StartDateTime > now);
                }
            }

            if (isAdmin)
            {
                counts.PendingApprovals = await _db.TrainerProfiles.CountAsync(t => t.ApprovedAt == null && t.User.AccountStatus == AccountStatus.PendingApproval);
            }

            return counts;
        }
    }
}
