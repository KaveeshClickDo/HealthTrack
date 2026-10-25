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
    /// Trainer feedback: given only to assigned clients (BR-08), optionally about one of their sessions.
    /// </summary>
    public class FeedbackService : IFeedbackService
    {
        private readonly ApplicationDbContext _db;
        private readonly IAssignmentService _assignments;
        private readonly INotificationService _notifications;
        private readonly IClock _clock;

        /// <summary>
        /// Creates the service.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="assignments">Checks that a trainer may reach a client (BR-08).</param>
        /// <param name="notifications">Tells clients about new feedback.</param>
        /// <param name="clock">Supplies the time.</param>
        public FeedbackService(ApplicationDbContext db, IAssignmentService assignments, INotificationService notifications, IClock clock)
        {
            _db = db;
            _assignments = assignments;
            _notifications = notifications;
            _clock = clock;
        }

        /// <inheritdoc />
        public async Task<FeedbackViewModel?> GetFeedbackFormAsync(string trainerUserId, int? clientId, int? workoutLogId)
        {
            if (await TrainerIdAsync(trainerUserId) is not { } trainerId)
            {
                return null;
            }

            if (clientId is { } id && !await _assignments.IsTrainerAssignedToClientAsync(trainerId, id))
            {
                return null;
            }

            if (workoutLogId is { } logId && (clientId is null || !await IsClientsSessionAsync(logId, clientId.Value)))
            {
                return null;
            }

            var model = new FeedbackViewModel
            {
                ClientId = clientId,
                WorkoutLogId = workoutLogId,
                Category = workoutLogId is null ? FeedbackCategory.Progress : FeedbackCategory.Workout
            };
            await FillFeedbackOptionsAsync(model, trainerUserId);
            return model;
        }

        /// <inheritdoc />
        public async Task FillFeedbackOptionsAsync(FeedbackViewModel model, string trainerUserId)
        {
            var clients = await _assignments.GetTrainerClientsAsync(trainerUserId);
            model.ClientOptions = clients.Select(c => new SelectOptionViewModel(c.ClientId, c.FullName)).ToList();

            var chosen = clients.FirstOrDefault(c => c.ClientId == model.ClientId);
            model.ClientName = chosen?.FullName;
            if (chosen is null)
            {
                model.SessionOptions = [];
                return;
            }

            var sessions = await _db.WorkoutLogs.AsNoTracking()
                .Where(l => l.ClientId == chosen.ClientId)
                .OrderByDescending(l => l.WorkoutDate)
                .ThenByDescending(l => l.WorkoutLogId)
                .Take(20)
                .Select(l => new
                {
                    l.WorkoutLogId,
                    l.WorkoutDate,
                    PlanTitle = l.WorkoutPlan != null ? l.WorkoutPlan.Title : null,
                    Exercises = l.Entries.Count,
                    Minutes = l.Entries.Sum(e => e.DurationMinutes)
                })
                .ToListAsync();

            model.SessionOptions = sessions
                .Select(s => new SelectOptionViewModel(s.WorkoutLogId,
                    string.Create(CultureInfo.InvariantCulture,
                        $"{s.WorkoutDate:dd MMM yyyy} – {s.PlanTitle ?? "Free workout"} ({s.Exercises} exercises, {s.Minutes} min)")))
                .ToList();
        }

        /// <inheritdoc />
        public async Task<int?> GiveFeedbackAsync(string trainerUserId, FeedbackViewModel model)
        {
            if (await TrainerIdAsync(trainerUserId) is not { } trainerId || model.ClientId is not { } clientId)
            {
                return null;
            }

            // BR-08: feedback goes only to the trainer's assigned clients, and only about their own sessions.
            if (!await _assignments.IsTrainerAssignedToClientAsync(trainerId, clientId))
            {
                return null;
            }

            if (model.WorkoutLogId is { } logId && !await IsClientsSessionAsync(logId, clientId))
            {
                return null;
            }

            var feedback = new Feedback
            {
                TrainerId = trainerId,
                ClientId = clientId,
                WorkoutLogId = model.WorkoutLogId,
                Category = model.Category!.Value,
                Message = model.Message.Trim(),
                Rating = model.Rating,
                CreatedAt = _clock.Now
            };
            _db.Feedback.Add(feedback);
            await _db.SaveChangesAsync();

            var names = await _db.Feedback.AsNoTracking()
                .Where(f => f.FeedbackId == feedback.FeedbackId)
                .Select(f => new { ClientUserId = f.Client.UserId, Trainer = f.Trainer.User.FirstName + " " + f.Trainer.User.LastName })
                .SingleAsync();
            await _notifications.NotifyAsync(names.ClientUserId, NotificationType.NewFeedback, $"New feedback from {names.Trainer}",
                $"{names.Trainer} left you {feedback.Category.GetDisplayName().ToLowerInvariant()} feedback.", "/Client/Feedback");
            return feedback.FeedbackId;
        }

        /// <inheritdoc />
        public async Task<PagedResult<FeedbackItemViewModel>> GetForClientAsync(int clientId, int page) =>
            await _db.Feedback.AsNoTracking()
                .Where(f => f.ClientId == clientId)
                .OrderByDescending(f => f.CreatedAt)
                .ThenByDescending(f => f.FeedbackId)
                .Select(f => new FeedbackItemViewModel
                {
                    FeedbackId = f.FeedbackId,
                    TrainerName = f.Trainer.User.FirstName + " " + f.Trainer.User.LastName,
                    Category = f.Category,
                    Message = f.Message,
                    Rating = f.Rating,
                    CreatedAt = f.CreatedAt,
                    WorkoutDate = f.WorkoutLog != null ? f.WorkoutLog.WorkoutDate : null
                })
                .ToPagedResultAsync(page);

        private Task<bool> IsClientsSessionAsync(int workoutLogId, int clientId) =>
            _db.WorkoutLogs.AnyAsync(l => l.WorkoutLogId == workoutLogId && l.ClientId == clientId);

        private Task<int?> TrainerIdAsync(string trainerUserId) =>
            _db.TrainerProfiles.Where(t => t.UserId == trainerUserId).Select(t => (int?)t.TrainerId).FirstOrDefaultAsync();
    }
}
