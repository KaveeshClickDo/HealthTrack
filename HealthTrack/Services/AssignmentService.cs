using HealthTrack.Data;
using HealthTrack.Models.Entities;
using HealthTrack.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Services
{
    /// <summary>
    /// Reads trainer–client assignments. Assigning and ending them follows in the administrator module.
    /// </summary>
    public class AssignmentService : IAssignmentService
    {
        private readonly ApplicationDbContext _db;

        /// <summary>
        /// Creates the service.
        /// </summary>
        /// <param name="db">The database context.</param>
        public AssignmentService(ApplicationDbContext db)
        {
            _db = db;
        }

        /// <inheritdoc />
        public async Task<TrainerProfile?> GetActiveTrainerAsync(int clientId) =>
            await _db.TrainerProfiles.AsNoTracking()
                .Include(t => t.User)
                .Where(t => t.Assignments.Any(a => a.ClientId == clientId && a.EndDate == null))
                .FirstOrDefaultAsync();

        /// <inheritdoc />
        public Task<bool> IsTrainerAssignedToClientAsync(int trainerId, int clientId) =>
            _db.TrainerAssignments.AnyAsync(a => a.TrainerId == trainerId && a.ClientId == clientId && a.EndDate == null);

        /// <inheritdoc />
        public async Task<IReadOnlyList<ClientProfile>> GetUnassignedClientsAsync() =>
            await _db.ClientProfiles.AsNoTracking()
                .Include(c => c.User)
                .Where(c => c.User.AccountStatus == AccountStatus.Active && !c.Assignments.Any(a => a.EndDate == null))
                .OrderBy(c => c.User.FirstName)
                .ThenBy(c => c.User.LastName)
                .ToListAsync();
    }
}
