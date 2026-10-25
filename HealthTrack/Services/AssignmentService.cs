using System.Globalization;
using HealthTrack.Data;
using HealthTrack.Helpers;
using HealthTrack.Models.Entities;
using HealthTrack.Models.Enums;
using HealthTrack.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Services
{
    /// <summary>
    /// Trainer–client assignments: the access checks other services rely on (BR-08) and the administrator's
    /// assigning and ending (BR-06, BR-07).
    /// </summary>
    public class AssignmentService : IAssignmentService
    {
        private readonly ApplicationDbContext _db;
        private readonly INotificationService _notifications;
        private readonly IAuditService _audit;
        private readonly IClock _clock;

        /// <summary>
        /// Creates the service.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="notifications">Tells clients and trainers about assignments.</param>
        /// <param name="audit">Records assignments.</param>
        /// <param name="clock">Supplies the date.</param>
        public AssignmentService(ApplicationDbContext db, INotificationService notifications, IAuditService audit, IClock clock)
        {
            _db = db;
            _notifications = notifications;
            _audit = audit;
            _clock = clock;
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

        /// <inheritdoc />
        public async Task<IReadOnlyList<TrainerClientViewModel>> GetTrainerClientsAsync(string trainerUserId) =>
            await _db.TrainerAssignments.AsNoTracking()
                .Where(a => a.Trainer.UserId == trainerUserId && a.EndDate == null)
                .OrderBy(a => a.Client.User.FirstName)
                .ThenBy(a => a.Client.User.LastName)
                .Select(a => new TrainerClientViewModel
                {
                    ClientId = a.ClientId,
                    FullName = a.Client.User.FirstName + " " + a.Client.User.LastName,
                    Email = a.Client.User.Email ?? string.Empty,
                    FitnessGoal = a.Client.FitnessGoal,
                    ActivityLevel = a.Client.ActivityLevel,
                    AssignedSince = a.StartDate,
                    LastWorkoutDate = a.Client.WorkoutLogs.Max(w => (DateTime?)w.WorkoutDate),
                    ActivePlanTitle = a.Client.WorkoutPlans
                        .Where(p => p.Status == PlanStatus.Active)
                        .Select(p => p.Title)
                        .FirstOrDefault()
                })
                .ToListAsync();

        /// <inheritdoc />
        public async Task<bool> AssignTrainerAsync(int clientId, int trainerId, DateTime startDate, string? notes, string actingAdminId)
        {
            var client = await _db.ClientProfiles
                .Include(c => c.User)
                .Include(c => c.Assignments.Where(a => a.EndDate == null)).ThenInclude(a => a.Trainer).ThenInclude(t => t.User)
                .FirstOrDefaultAsync(c => c.ClientId == clientId);
            var trainer = await _db.TrainerProfiles
                .Include(t => t.User)
                .Include(t => t.Assignments.Where(a => a.EndDate == null))
                .FirstOrDefaultAsync(t => t.TrainerId == trainerId);
            if (client is null || trainer is null)
            {
                return false;
            }

            var start = startDate.Date;
            if (start > _clock.Today)
            {
                throw new BusinessRuleException("BR-06", "An assignment can't start in the future.");
            }

            if (client.User.AccountStatus != AccountStatus.Active)
            {
                throw new BusinessRuleException("BR-06", "Only active client accounts can be given a trainer.");
            }

            // BR-07: only approved, active trainers with room can be assigned.
            if (!trainer.IsApproved || trainer.User.AccountStatus != AccountStatus.Active)
            {
                throw new BusinessRuleException("BR-07", "Only approved, active trainers can be assigned.");
            }

            var current = client.GetActiveAssignment();
            if (current?.TrainerId == trainer.TrainerId)
            {
                throw new BusinessRuleException("BR-06", $"{client.User.FullName} is already assigned to {trainer.User.FullName}.");
            }

            if (!trainer.HasCapacity())
            {
                throw new BusinessRuleException("BR-07",
                    $"{trainer.User.FullName} already has {trainer.ActiveClientCount()} of {trainer.MaxClients} clients.");
            }

            await using var transaction = await _db.Database.BeginTransactionAsync();

            // BR-06: a client has one active assignment, so the current one ends the day the new one starts.
            current?.End(start);

            var assignment = new TrainerAssignment
            {
                ClientId = client.ClientId,
                TrainerId = trainer.TrainerId,
                StartDate = start,
                Notes = notes.NullIfBlank()
            };
            _db.TrainerAssignments.Add(assignment);
            await _db.SaveChangesAsync();

            await _notifications.NotifyAsync(client.UserId, NotificationType.AccountUpdate, $"Your trainer is {trainer.User.FullName}",
                $"{trainer.User.FullName} ({trainer.ProfessionalType.GetDisplayName().ToLowerInvariant()}) will now plan and follow your " +
                "training, and can see your plans, logs and progress.", "/Client/Dashboard");
            await _notifications.NotifyAsync(trainer.UserId, NotificationType.AccountUpdate, "New client assigned",
                $"{client.User.FullName} has been assigned to you from {start:dd MMM yyyy}.", "/Trainer/Clients");
            if (current is not null)
            {
                await _notifications.NotifyAsync(current.Trainer.UserId, NotificationType.AccountUpdate, "Client reassigned",
                    $"{client.User.FullName} has moved to another trainer and is no longer assigned to you.");
            }

            await _audit.LogAsync(actingAdminId, AuditAction.Assign, nameof(TrainerAssignment),
                assignment.AssignmentId.ToString(CultureInfo.InvariantCulture),
                $"{client.User.FullName} assigned to {trainer.User.FullName}" +
                (current is null ? "." : $"; the assignment to {current.Trainer.User.FullName} ended."));

            await transaction.CommitAsync();
            return true;
        }

        /// <inheritdoc />
        public async Task<bool> EndAssignmentAsync(int assignmentId, DateTime endDate, string actingAdminId)
        {
            var assignment = await _db.TrainerAssignments
                .Include(a => a.Client).ThenInclude(c => c.User)
                .Include(a => a.Trainer).ThenInclude(t => t.User)
                .FirstOrDefaultAsync(a => a.AssignmentId == assignmentId);
            if (assignment is null)
            {
                return false;
            }

            // A future end date would make the assignment look ended today, so it isn't allowed.
            if (endDate.Date > _clock.Today)
            {
                throw new BusinessRuleException("BR-06", "An assignment can't be ended on a future date.");
            }

            await using var transaction = await _db.Database.BeginTransactionAsync();

            assignment.End(endDate);
            await _db.SaveChangesAsync();

            await _notifications.NotifyAsync(assignment.Client.UserId, NotificationType.AccountUpdate, "Trainer assignment ended",
                $"{assignment.Trainer.User.FullName} is no longer your trainer. An administrator can assign you a new one.");
            await _notifications.NotifyAsync(assignment.Trainer.UserId, NotificationType.AccountUpdate, "Client assignment ended",
                $"{assignment.Client.User.FullName} is no longer assigned to you.");
            await _audit.LogAsync(actingAdminId, AuditAction.Update, nameof(TrainerAssignment),
                assignment.AssignmentId.ToString(CultureInfo.InvariantCulture),
                $"Assignment of {assignment.Client.User.FullName} to {assignment.Trainer.User.FullName} ended.");

            await transaction.CommitAsync();
            return true;
        }

        /// <inheritdoc />
        public async Task<PagedResult<AssignmentListItemViewModel>> GetAssignmentsAsync(AssignmentFilterViewModel filter, int page)
        {
            var query = _db.TrainerAssignments.AsNoTracking();

            if (filter.ActiveOnly)
            {
                query = query.Where(a => a.EndDate == null);
            }

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var term = filter.Search.Trim().ToUpperInvariant();
                query = query.Where(a => (a.Client.User.NormalizedEmail != null && a.Client.User.NormalizedEmail.Contains(term))
                    || (a.Client.User.FirstName + " " + a.Client.User.LastName).ToUpper().Contains(term)
                    || (a.Trainer.User.FirstName + " " + a.Trainer.User.LastName).ToUpper().Contains(term));
            }

            return await query
                .OrderBy(a => a.EndDate == null ? 0 : 1)
                .ThenByDescending(a => a.StartDate)
                .ThenByDescending(a => a.AssignmentId)
                .Select(a => new AssignmentListItemViewModel
                {
                    AssignmentId = a.AssignmentId,
                    ClientName = a.Client.User.FirstName + " " + a.Client.User.LastName,
                    ClientEmail = a.Client.User.Email ?? string.Empty,
                    TrainerName = a.Trainer.User.FirstName + " " + a.Trainer.User.LastName,
                    ProfessionalType = a.Trainer.ProfessionalType,
                    StartDate = a.StartDate,
                    EndDate = a.EndDate,
                    Notes = a.Notes
                })
                .ToPagedResultAsync(page);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<TrainerLoadViewModel>> GetTrainerLoadAsync() =>
            await _db.TrainerProfiles.AsNoTracking()
                .Where(t => t.ApprovedAt != null)
                .OrderBy(t => t.User.FirstName)
                .ThenBy(t => t.User.LastName)
                .Select(t => new TrainerLoadViewModel
                {
                    TrainerId = t.TrainerId,
                    FullName = t.User.FirstName + " " + t.User.LastName,
                    ProfessionalType = t.ProfessionalType,
                    AccountStatus = t.User.AccountStatus,
                    ActiveClients = t.Assignments.Count(a => a.EndDate == null),
                    MaxClients = t.MaxClients
                })
                .ToListAsync();

        /// <inheritdoc />
        public async Task<AssignmentsOverviewViewModel> GetOverviewAsync(AssignmentFilterViewModel filter, int page) => new()
        {
            Filter = filter,
            Assignments = await GetAssignmentsAsync(filter, page),
            UnassignedClients = await GetUnassignedClientsAsync(),
            TrainerLoad = await GetTrainerLoadAsync(),
            Today = _clock.Today
        };

        /// <inheritdoc />
        public async Task<AssignTrainerViewModel> GetAssignFormAsync(int? clientId)
        {
            var model = new AssignTrainerViewModel { ClientId = clientId, StartDate = _clock.Today };
            await FillAssignOptionsAsync(model);
            return model;
        }

        /// <inheritdoc />
        public async Task FillAssignOptionsAsync(AssignTrainerViewModel model)
        {
            var clients = await _db.ClientProfiles.AsNoTracking()
                .Where(c => c.User.AccountStatus == AccountStatus.Active)
                .OrderBy(c => c.User.FirstName)
                .ThenBy(c => c.User.LastName)
                .Select(c => new
                {
                    c.ClientId,
                    Name = c.User.FirstName + " " + c.User.LastName,
                    c.User.Email,
                    Trainer = c.Assignments.Where(a => a.EndDate == null)
                        .Select(a => a.Trainer.User.FirstName + " " + a.Trainer.User.LastName)
                        .FirstOrDefault()
                })
                .ToListAsync();

            // BR-07: only approved, active trainers with room are offered.
            var trainers = await _db.TrainerProfiles.AsNoTracking()
                .Where(t => t.ApprovedAt != null && t.User.AccountStatus == AccountStatus.Active)
                .OrderBy(t => t.User.FirstName)
                .ThenBy(t => t.User.LastName)
                .Select(t => new
                {
                    t.TrainerId,
                    Name = t.User.FirstName + " " + t.User.LastName,
                    t.ProfessionalType,
                    ActiveClients = t.Assignments.Count(a => a.EndDate == null),
                    t.MaxClients
                })
                .ToListAsync();

            model.ClientOptions = clients
                .Select(c => new SelectOptionViewModel(c.ClientId,
                    $"{c.Name} ({c.Email}) – {(c.Trainer is null ? "no trainer" : "with " + c.Trainer)}"))
                .ToList();
            model.TrainerOptions = trainers
                .Where(t => t.ActiveClients < t.MaxClients)
                .Select(t => new SelectOptionViewModel(t.TrainerId,
                    $"{t.Name} – {t.ProfessionalType.GetDisplayName()} ({t.ActiveClients} of {t.MaxClients} clients)"))
                .ToList();
        }
    }
}
