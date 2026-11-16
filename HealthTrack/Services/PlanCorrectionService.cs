using HealthTrack.Data;
using HealthTrack.Helpers;
using HealthTrack.Models.Entities;
using HealthTrack.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Services
{
    /// <summary>
    /// Moves a plan that was assigned to the wrong client to the right one (EXT-02, BR-24).
    /// </summary>
    public class PlanCorrectionService : IPlanCorrectionService
    {
        private readonly ApplicationDbContext _db;
        private readonly INotificationService _notifications;
        private readonly IAuditService _audit;
        private readonly IClock _clock;

        /// <summary>
        /// Creates the service.
        /// </summary>
        /// <param name="db">The database.</param>
        /// <param name="notifications">Tells the professional and the client who had the plan.</param>
        /// <param name="audit">Records the correction and its reason.</param>
        /// <param name="clock">The current time.</param>
        public PlanCorrectionService(ApplicationDbContext db, INotificationService notifications, IAuditService audit, IClock clock)
        {
            _db = db;
            _notifications = notifications;
            _audit = audit;
            _clock = clock;
        }

        /// <inheritdoc />
        public async Task<string?> MovePlanAsync(bool isDietPlan, int planId, int newClientId, string reason, string adminUserId)
        {
            // The two plan kinds differ only in their table; the rules are the same.
            WorkoutPlan? workoutPlan = null;
            DietPlan? dietPlan = null;
            if (isDietPlan)
            {
                dietPlan = await _db.DietPlans.Include(p => p.Trainer).ThenInclude(t => t.User)
                    .Include(p => p.Client).ThenInclude(c => c.User)
                    .FirstOrDefaultAsync(p => p.DietPlanId == planId);
            }
            else
            {
                workoutPlan = await _db.WorkoutPlans.Include(p => p.Trainer).ThenInclude(t => t.User)
                    .Include(p => p.Client).ThenInclude(c => c!.User)
                    .FirstOrDefaultAsync(p => p.WorkoutPlanId == planId && !p.IsTemplate);
            }

            if (workoutPlan is null && dietPlan is null)
            {
                return null;
            }

            var title = workoutPlan?.Title ?? dietPlan!.Title;
            var status = workoutPlan?.Status ?? dietPlan!.Status;
            var trainer = workoutPlan?.Trainer ?? dietPlan!.Trainer;
            var oldClient = workoutPlan?.Client ?? dietPlan!.Client;
            if (oldClient is null)
            {
                return null;
            }

            var trimmedReason = reason?.Trim();
            if (string.IsNullOrEmpty(trimmedReason))
            {
                throw new BusinessRuleException("BR-24", "Give the reason for the correction.");
            }

            // BR-24: completed and archived plans are history (BR-21), and a plan the client has used is theirs.
            if (status is not (PlanStatus.Draft or PlanStatus.Active))
            {
                throw new BusinessRuleException("BR-24", "Only a draft or active plan can be moved; finished plans are kept as history.");
            }

            var used = isDietPlan
                ? await _db.MealLogs.AnyAsync(l => l.DietPlanMeal != null && l.DietPlanMeal.DietPlanId == planId)
                : await _db.WorkoutLogs.AnyAsync(l => l.WorkoutPlanId == planId);
            if (used)
            {
                throw new BusinessRuleException("BR-24", $"{oldClient.User.FullName} has already logged against this plan, so it stays with them.");
            }

            if (newClientId == oldClient.ClientId)
            {
                throw new BusinessRuleException("BR-24", "Choose a different client from the one who has the plan now.");
            }

            // BR-24 with BR-07: the plan's professional may only plan for clients assigned to them.
            var newClient = await _db.ClientProfiles.Include(c => c.User)
                .Where(c => c.ClientId == newClientId
                    && c.Assignments.Any(a => a.TrainerId == trainer.TrainerId && a.EndDate == null))
                .FirstOrDefaultAsync();
            if (newClient is null)
            {
                throw new BusinessRuleException("BR-24", $"The plan can only move to a client assigned to {trainer.User.FullName}.");
            }

            var now = _clock.Now;
            await using var transaction = await _db.Database.BeginTransactionAsync();

            // The plan goes back to a draft: the administrator only corrects the link; the professional checks the plan
            // and activates it for the right client (which completes that client's current plan, BR-09).
            if (workoutPlan is not null)
            {
                workoutPlan.ClientId = newClient.ClientId;
                workoutPlan.Status = PlanStatus.Draft;
                workoutPlan.UpdatedAt = now;
            }
            else
            {
                dietPlan!.ClientId = newClient.ClientId;
                dietPlan.Status = PlanStatus.Draft;
                dietPlan.UpdatedAt = now;
            }

            await _db.SaveChangesAsync();

            var kind = isDietPlan ? "diet" : "workout";
            var editUrl = isDietPlan ? $"/Trainer/DietPlans/Edit/{planId}" : $"/Trainer/WorkoutPlans/Edit/{planId}";
            await _notifications.NotifyAsync(trainer.UserId, NotificationType.PlanAssigned, $"A plan was moved to {newClient.User.FullName}",
                $"An administrator moved your {kind} plan \"{title}\" from {oldClient.User.FullName} to {newClient.User.FullName}, because it had been " +
                $"assigned to the wrong client. It's a draft again: check it and activate it for {newClient.User.FirstName}. Reason: {trimmedReason}",
                editUrl);
            await _notifications.NotifyAsync(oldClient.UserId, NotificationType.PlanAssigned, "A plan was removed from your account",
                $"The {kind} plan \"{title}\" was meant for another client and has been removed from your account. " +
                $"{trainer.User.FirstName} will be in touch about your own plan.");

            await _audit.LogAsync(adminUserId, AuditAction.Update, isDietPlan ? nameof(DietPlan) : nameof(WorkoutPlan), planId.ToString(),
                $"Plan \"{title}\" moved from {oldClient.User.Email} to {newClient.User.Email} (assigned to the wrong client); now a draft. Reason: {trimmedReason}".Truncate(500));

            await transaction.CommitAsync();
            return oldClient.UserId;
        }
    }
}
