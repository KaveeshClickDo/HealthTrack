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
    /// Workout plans, routine templates and workout logging.
    /// </summary>
    public class WorkoutService : IWorkoutService
    {
        private readonly ApplicationDbContext _db;
        private readonly IAssignmentService _assignments;
        private readonly INotificationService _notifications;
        private readonly IAuditService _audit;
        private readonly IClock _clock;

        /// <summary>
        /// Creates the service.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="assignments">Checks that a trainer may reach a client (BR-08).</param>
        /// <param name="notifications">Tells clients about new plans.</param>
        /// <param name="audit">Records plan changes and logs.</param>
        /// <param name="clock">Supplies the time.</param>
        public WorkoutService(ApplicationDbContext db, IAssignmentService assignments, INotificationService notifications,
            IAuditService audit, IClock clock)
        {
            _db = db;
            _assignments = assignments;
            _notifications = notifications;
            _audit = audit;
            _clock = clock;
        }

        /// <inheritdoc />
        public async Task<WorkoutPlanViewModel?> GetCreatePlanFormAsync(string trainerUserId, int? clientId, bool isTemplate)
        {
            // BR-08: a client given in the URL must be one of the trainer's (the diet plan form answers the same way).
            if (!isTemplate && clientId is { } id
                && (await TrainerIdAsync(trainerUserId) is not { } trainerId || !await _assignments.IsTrainerAssignedToClientAsync(trainerId, id)))
            {
                return null;
            }

            var model = new WorkoutPlanViewModel
            {
                IsTemplate = isTemplate,
                ClientId = isTemplate ? null : clientId,
                Difficulty = DifficultyLevel.Beginner,
                StartDate = isTemplate ? null : _clock.Today
            };
            await FillPlanOptionsAsync(model, trainerUserId);
            return model;
        }

        /// <inheritdoc />
        public async Task FillPlanOptionsAsync(WorkoutPlanViewModel model, string trainerUserId) =>
            model.ClientOptions = await ClientOptionsAsync(trainerUserId);

        /// <inheritdoc />
        public async Task<int?> CreatePlanAsync(string trainerUserId, WorkoutPlanViewModel model)
        {
            if (await TrainerIdAsync(trainerUserId) is not { } trainerId)
            {
                return null;
            }

            int? clientId = null;
            if (!model.IsTemplate)
            {
                // BR-08: trainers plan only for their assigned clients.
                if (model.ClientId is not { } id || !await _assignments.IsTrainerAssignedToClientAsync(trainerId, id))
                {
                    return null;
                }

                clientId = id;
            }

            var plan = new WorkoutPlan
            {
                TrainerId = trainerId,
                ClientId = clientId,
                IsTemplate = model.IsTemplate,
                Title = model.Title.Trim(),
                Description = model.Description.NullIfBlank(),
                Difficulty = model.Difficulty!.Value,
                StartDate = model.IsTemplate ? null : model.StartDate?.Date,
                EndDate = model.IsTemplate ? null : model.EndDate?.Date,
                Status = PlanStatus.Draft,
                CreatedAt = _clock.Now
            };
            EnsureValidDates(plan);

            _db.WorkoutPlans.Add(plan);
            await _db.SaveChangesAsync();
            await _audit.LogAsync(trainerUserId, AuditAction.Create, nameof(WorkoutPlan), Key(plan.WorkoutPlanId),
                $"{(plan.IsTemplate ? "Template" : "Workout plan")} created: {plan.Title}.");
            return plan.WorkoutPlanId;
        }

        /// <inheritdoc />
        public async Task<PagedResult<WorkoutPlanListItemViewModel>> GetTrainerPlansAsync(string trainerUserId, int page)
        {
            var trainerId = await TrainerIdAsync(trainerUserId) ?? 0;

            return await _db.WorkoutPlans.AsNoTracking()
                .Where(p => p.TrainerId == trainerId && !p.IsTemplate)
                .OrderBy(p => p.Status == PlanStatus.Archived ? 2 : p.Status == PlanStatus.Completed ? 1 : 0)
                .ThenByDescending(p => p.CreatedAt)
                .Select(p => new WorkoutPlanListItemViewModel
                {
                    WorkoutPlanId = p.WorkoutPlanId,
                    Title = p.Title,
                    ClientName = p.Client != null ? p.Client.User.FirstName + " " + p.Client.User.LastName : string.Empty,
                    Status = p.Status,
                    StartDate = p.StartDate,
                    EndDate = p.EndDate,
                    ExerciseCount = p.Exercises.Count,
                    SourceTemplateTitle = p.SourceTemplate != null ? p.SourceTemplate.Title : null,
                    ClientId = p.ClientId,
                    Difficulty = p.Difficulty,
                    FirstExerciseName = p.Exercises.OrderBy(e => e.DayOfWeek).ThenBy(e => e.SortOrder).Select(e => e.Exercise.Name).FirstOrDefault(),
                    FirstExerciseCategory = p.Exercises.OrderBy(e => e.DayOfWeek).ThenBy(e => e.SortOrder).Select(e => (ExerciseCategory?)e.Exercise.Category).FirstOrDefault()
                })
                .ToPagedResultAsync(page);
        }

        /// <inheritdoc />
        public async Task<WorkoutPlanEditViewModel?> GetPlanForEditAsync(int planId, string trainerUserId)
        {
            var plan = await LoadOwnPlanAsync(planId, trainerUserId);
            if (plan is null)
            {
                return null;
            }

            var model = new WorkoutPlanEditViewModel
            {
                Title = plan.Title,
                Description = plan.Description,
                Difficulty = plan.Difficulty,
                StartDate = plan.StartDate,
                EndDate = plan.EndDate,
                Lines = OrderLines(plan.Exercises).Select(l => new PlanLineEditViewModel
                {
                    WorkoutPlanExerciseId = l.WorkoutPlanExerciseId,
                    Sets = l.Sets,
                    Reps = l.Reps,
                    DurationMinutes = l.DurationMinutes,
                    RestSeconds = l.RestSeconds,
                    Notes = l.Notes
                }).ToList()
            };
            await FillEditDisplayAsync(model, plan);
            return model;
        }

        /// <inheritdoc />
        public async Task<bool> FillEditDisplayAsync(WorkoutPlanEditViewModel model, int planId, string trainerUserId)
        {
            var plan = await LoadOwnPlanAsync(planId, trainerUserId);
            if (plan is null)
            {
                return false;
            }

            await FillEditDisplayAsync(model, plan);
            return true;
        }

        /// <inheritdoc />
        public async Task<bool> UpdatePlanAsync(int planId, string trainerUserId, WorkoutPlanEditViewModel model)
        {
            var plan = await LoadOwnPlanAsync(planId, trainerUserId);
            if (plan is null)
            {
                return false;
            }

            EnsureEditable(plan);

            plan.Title = model.Title.Trim();
            plan.Description = model.Description.NullIfBlank();
            plan.Difficulty = model.Difficulty!.Value;
            if (!plan.IsTemplate)
            {
                plan.StartDate = model.StartDate?.Date;
                plan.EndDate = model.EndDate?.Date;
            }

            EnsureValidDates(plan);

            // Lines are matched by key; keys from another plan are ignored.
            foreach (var posted in model.Lines)
            {
                var line = plan.Exercises.FirstOrDefault(l => l.WorkoutPlanExerciseId == posted.WorkoutPlanExerciseId);
                if (line is null)
                {
                    continue;
                }

                if (posted.Remove)
                {
                    _db.WorkoutPlanExercises.Remove(line);
                    continue;
                }

                line.Sets = posted.Sets;
                line.Reps = posted.Reps;
                line.DurationMinutes = posted.DurationMinutes;
                line.RestSeconds = posted.RestSeconds;
                line.Notes = posted.Notes.NullIfBlank();
                if (!line.IsValidPrescription())
                {
                    throw new BusinessRuleException("BR-11", PlanExerciseViewModel.PrescriptionMessage);
                }
            }

            // BR-10: an active plan must keep at least one exercise.
            if (plan.Status == PlanStatus.Active && plan.Exercises.All(l => _db.Entry(l).State == EntityState.Deleted))
            {
                throw new BusinessRuleException("BR-10", "An active plan needs at least one exercise. Archive the plan instead.");
            }

            plan.UpdatedAt = _clock.Now;
            await _db.SaveChangesAsync();
            await _audit.LogAsync(trainerUserId, AuditAction.Update, nameof(WorkoutPlan), Key(plan.WorkoutPlanId),
                $"Workout plan updated: {plan.Title}.");
            return true;
        }

        /// <inheritdoc />
        public async Task<bool> AddExerciseAsync(int planId, string trainerUserId, PlanExerciseViewModel model)
        {
            var plan = await LoadOwnPlanAsync(planId, trainerUserId);
            if (plan is null)
            {
                return false;
            }

            EnsureEditable(plan);

            var exercise = await _db.Exercises.AsNoTracking().FirstOrDefaultAsync(e => e.ExerciseId == model.ExerciseId);
            if (exercise is null || !exercise.IsActive)
            {
                throw new BusinessRuleException("BR-21", "That exercise isn't in the active library.");
            }

            var day = model.DayOfWeek!.Value;
            var line = new WorkoutPlanExercise
            {
                ExerciseId = exercise.ExerciseId,
                DayOfWeek = day,
                SortOrder = plan.Exercises.Where(l => l.DayOfWeek == day).Select(l => l.SortOrder).DefaultIfEmpty(0).Max() + 1,
                Sets = model.Sets,
                Reps = model.Reps,
                DurationMinutes = model.DurationMinutes,
                RestSeconds = model.RestSeconds,
                Notes = model.Notes.NullIfBlank()
            };

            // BR-11: every plan exercise has sets and reps, or a duration.
            if (!line.IsValidPrescription())
            {
                throw new BusinessRuleException("BR-11", PlanExerciseViewModel.PrescriptionMessage);
            }

            plan.Exercises.Add(line);
            plan.UpdatedAt = _clock.Now;
            await _db.SaveChangesAsync();
            await _audit.LogAsync(trainerUserId, AuditAction.Update, nameof(WorkoutPlan), Key(plan.WorkoutPlanId),
                $"{exercise.Name} added to {plan.Title} on {day}.");
            return true;
        }

        /// <inheritdoc />
        public async Task<bool> ArchivePlanAsync(int planId, string trainerUserId)
        {
            var plan = await LoadOwnPlanAsync(planId, trainerUserId);
            if (plan is null)
            {
                return false;
            }

            plan.Archive();
            plan.UpdatedAt = _clock.Now;
            await _db.SaveChangesAsync();
            await _audit.LogAsync(trainerUserId, AuditAction.Update, nameof(WorkoutPlan), Key(plan.WorkoutPlanId),
                $"Workout plan archived: {plan.Title}.");
            return true;
        }

        /// <inheritdoc />
        public async Task<int?> SaveAsTemplateAsync(int planId, string trainerUserId)
        {
            var plan = await LoadOwnPlanAsync(planId, trainerUserId);
            if (plan is null)
            {
                return null;
            }

            if (plan.Exercises.Count == 0)
            {
                throw new BusinessRuleException("BR-10", "Add at least one exercise before saving the plan as a template.");
            }

            // UC39: a copy, so the client plan itself doesn't change.
            var template = plan.CreateTemplateCopy(plan.TrainerId);
            template.CreatedAt = _clock.Now;
            _db.WorkoutPlans.Add(template);
            await _db.SaveChangesAsync();

            await _audit.LogAsync(trainerUserId, AuditAction.Create, nameof(WorkoutPlan), Key(template.WorkoutPlanId),
                $"Template saved from the plan {plan.Title}.");
            return template.WorkoutPlanId;
        }

        /// <inheritdoc />
        public async Task<bool> ActivatePlanAsync(int planId, string trainerUserId)
        {
            var plan = await LoadOwnPlanAsync(planId, trainerUserId);
            if (plan is null)
            {
                return false;
            }

            await using var transaction = await _db.Database.BeginTransactionAsync();
            await ActivateCoreAsync(plan, trainerUserId);
            await transaction.CommitAsync();
            return true;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<TemplateListItemViewModel>> GetTemplatesAsync(string trainerUserId)
        {
            var trainerId = await TrainerIdAsync(trainerUserId) ?? 0;

            return await _db.WorkoutPlans.AsNoTracking()
                .Where(p => p.IsTemplate && p.Status != PlanStatus.Archived)
                .OrderBy(p => p.Title)
                .Select(p => new TemplateListItemViewModel
                {
                    WorkoutPlanId = p.WorkoutPlanId,
                    Title = p.Title,
                    Description = p.Description,
                    Difficulty = p.Difficulty,
                    AuthorName = p.Trainer.User.FirstName + " " + p.Trainer.User.LastName,
                    IsOwn = p.TrainerId == trainerId,
                    ExerciseCount = p.Exercises.Count,
                    DayCount = p.Exercises.Select(e => e.DayOfWeek).Distinct().Count(),
                    TimesAssigned = p.Copies.Count,
                    FirstExerciseName = p.Exercises.OrderBy(e => e.DayOfWeek).ThenBy(e => e.SortOrder).Select(e => e.Exercise.Name).FirstOrDefault(),
                    FirstExerciseCategory = p.Exercises.OrderBy(e => e.DayOfWeek).ThenBy(e => e.SortOrder).Select(e => (ExerciseCategory?)e.Exercise.Category).FirstOrDefault(),
                    Days = p.Exercises.Select(e => e.DayOfWeek).Distinct().ToList()
                })
                .ToListAsync();
        }

        /// <inheritdoc />
        public async Task<AssignTemplateViewModel?> GetAssignTemplateFormAsync(int templateId, string trainerUserId, int? clientId)
        {
            if (await TrainerIdAsync(trainerUserId) is not { } trainerId)
            {
                return null;
            }

            if (clientId is { } id && !await _assignments.IsTrainerAssignedToClientAsync(trainerId, id))
            {
                return null;
            }

            var model = new AssignTemplateViewModel { TemplateId = templateId, ClientId = clientId, StartDate = _clock.Today };
            return await FillAssignTemplateDisplayAsync(model, templateId, trainerUserId) ? model : null;
        }

        /// <inheritdoc />
        public async Task<bool> FillAssignTemplateDisplayAsync(AssignTemplateViewModel model, int templateId, string trainerUserId)
        {
            var template = await _db.WorkoutPlans.AsNoTracking()
                .Include(p => p.Exercises).ThenInclude(e => e.Exercise)
                .Include(p => p.Trainer).ThenInclude(t => t.User)
                .FirstOrDefaultAsync(p => p.WorkoutPlanId == templateId && p.IsTemplate && p.Status != PlanStatus.Archived);
            if (template is null)
            {
                return false;
            }

            model.TemplateId = template.WorkoutPlanId;
            model.TemplateTitle = template.Title;
            model.Difficulty = template.Difficulty;
            model.AuthorName = template.Trainer.User.FullName;
            model.Days = WeekOf(template).Where(d => !d.IsRestDay).ToList();
            model.ClientOptions = await ClientOptionsAsync(trainerUserId);
            return true;
        }

        /// <inheritdoc />
        public async Task<int?> AssignTemplateAsync(int templateId, int clientId, DateTime startDate, bool activateNow, string trainerUserId)
        {
            if (await TrainerIdAsync(trainerUserId) is not { } trainerId)
            {
                return null;
            }

            var template = await _db.WorkoutPlans.AsNoTracking()
                .Include(p => p.Exercises)
                .FirstOrDefaultAsync(p => p.WorkoutPlanId == templateId && p.IsTemplate && p.Status != PlanStatus.Archived);

            // Any trainer may use any template, but only for their own assigned clients (BR-08).
            if (template is null || !await _assignments.IsTrainerAssignedToClientAsync(trainerId, clientId))
            {
                return null;
            }

            await using var transaction = await _db.Database.BeginTransactionAsync();

            // BR-12: the client gets a copy; later edits never change the template.
            var plan = template.CreateCopyForClient(clientId, trainerId, startDate);
            plan.CreatedAt = _clock.Now;
            _db.WorkoutPlans.Add(plan);
            await _db.SaveChangesAsync();
            await _audit.LogAsync(trainerUserId, AuditAction.Create, nameof(WorkoutPlan), Key(plan.WorkoutPlanId),
                $"Template {template.Title} assigned to client {clientId}.");

            if (activateNow)
            {
                await ActivateCoreAsync(plan, trainerUserId);
            }

            await transaction.CommitAsync();
            return plan.WorkoutPlanId;
        }

        /// <inheritdoc />
        public async Task<WorkoutWeekViewModel> GetWeekAsync(int clientId)
        {
            var plan = await ActivePlanQuery(clientId)
                .Include(p => p.Trainer).ThenInclude(t => t.User)
                .FirstOrDefaultAsync();
            if (plan is null)
            {
                return new WorkoutWeekViewModel { HasPlan = false };
            }

            // This week, Monday to Sunday, with the days a workout was logged (the week strip on the plan page).
            var today = _clock.Today;
            var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
            var sunday = monday.AddDays(6);
            var logged = (await _db.WorkoutLogs.AsNoTracking()
                    .Where(l => l.ClientId == clientId && l.WorkoutDate >= monday && l.WorkoutDate <= sunday)
                    .Select(l => l.WorkoutDate)
                    .ToListAsync())
                .ToHashSet();

            return new WorkoutWeekViewModel
            {
                HasPlan = true,
                Title = plan.Title,
                Description = plan.Description,
                Difficulty = plan.Difficulty,
                TrainerName = plan.Trainer.User.FullName,
                StartDate = plan.StartDate,
                EndDate = plan.EndDate,
                Days = WeekOf(plan, monday, logged)
            };
        }

        /// <inheritdoc />
        public async Task<LogWorkoutViewModel> GetTodaysWorkoutAsync(int clientId, DateTime today)
        {
            var date = today.Date;
            var model = new LogWorkoutViewModel
            {
                WorkoutDate = date,
                AlreadyLoggedToday = await _db.WorkoutLogs.AnyAsync(l => l.ClientId == clientId && l.WorkoutDate == date)
            };

            // The active plan counts only between its start and end dates.
            var plan = await ActivePlanQuery(clientId).FirstOrDefaultAsync();
            if (plan is not null && plan.StartDate <= date && (plan.EndDate is null || plan.EndDate >= date))
            {
                var lines = plan.GetExercisesFor(date.DayOfWeek);
                model.HasActivePlan = true;
                model.IsRestDay = lines.Count == 0;
                model.WorkoutPlanId = plan.WorkoutPlanId;
                model.PlanTitle = plan.Title;
                model.Entries = lines.Select(l => new LogEntryViewModel
                {
                    Include = true,
                    ExerciseId = l.ExerciseId,
                    // The plan's minutes, or about 2 minutes a set when it only gives sets and reps.
                    DurationMinutes = l.DurationMinutes ?? (l.Sets is > 0 ? l.Sets * 2 : 10),
                    Sets = l.Sets,
                    Reps = l.Reps,
                    Target = WorkoutText.Prescription(l.Sets, l.Reps, l.DurationMinutes, l.RestSeconds)
                }).ToList();
            }

            await FillLogOptionsAsync(model);
            return model;
        }

        /// <inheritdoc />
        public async Task<LogWorkoutViewModel> GetLogFormAsync()
        {
            var model = new LogWorkoutViewModel { WorkoutDate = _clock.Today, Entries = [new LogEntryViewModel { Include = true }] };
            await FillLogOptionsAsync(model);
            return model;
        }

        /// <inheritdoc />
        public async Task FillLogOptionsAsync(LogWorkoutViewModel model)
        {
            // Exercises already on the form stay choosable even if deactivated since (for example in today's plan).
            var onForm = model.Entries.Where(e => e.ExerciseId is not null).Select(e => e.ExerciseId!.Value).Distinct().ToList();
            var exercises = await _db.Exercises.AsNoTracking()
                .Where(e => e.IsActive || onForm.Contains(e.ExerciseId))
                .OrderBy(e => e.Category)
                .ThenBy(e => e.Name)
                .ToListAsync();

            model.ExerciseOptions = exercises
                .Select(e => new SelectOptionViewModel(e.ExerciseId, e.Name, e.Category.GetDisplayName()))
                .ToList();
        }

        /// <inheritdoc />
        public async Task<int?> LogWorkoutAsync(string clientUserId, LogWorkoutViewModel model)
        {
            var clientId = await _db.ClientProfiles.Where(c => c.UserId == clientUserId).Select(c => (int?)c.ClientId).FirstOrDefaultAsync();
            if (clientId is null)
            {
                return null;
            }

            // BR-13: not in the future, and at least one exercise.
            var date = model.WorkoutDate!.Value.Date;
            if (date > _clock.Today)
            {
                throw new BusinessRuleException("BR-13", "A workout can't be logged for a future date.");
            }

            var entries = model.Entries.Where(e => e.Include).ToList();
            if (entries.Count == 0)
            {
                throw new BusinessRuleException("BR-13", LogWorkoutViewModel.NoEntriesMessage);
            }

            WorkoutPlan? plan = null;
            if (model.WorkoutPlanId is { } planId)
            {
                plan = await _db.WorkoutPlans.AsNoTracking().Include(p => p.Exercises)
                    .FirstOrDefaultAsync(p => p.WorkoutPlanId == planId && p.ClientId == clientId);
                if (plan is null)
                {
                    return null;
                }
            }

            var exerciseIds = entries.Select(e => e.ExerciseId!.Value).Distinct().ToList();
            var exercises = await _db.Exercises.AsNoTracking()
                .Where(e => exerciseIds.Contains(e.ExerciseId))
                .ToDictionaryAsync(e => e.ExerciseId);
            foreach (var id in exerciseIds)
            {
                // BR-21: deactivated exercises can't be picked, unless they are in the plan being followed.
                var inPlan = plan?.Exercises.Any(l => l.ExerciseId == id) ?? false;
                if (!exercises.TryGetValue(id, out var exercise) || (!exercise.IsActive && !inPlan))
                {
                    throw new BusinessRuleException("BR-21", "One of the exercises is no longer in the library.");
                }
            }

            // BR-14: calories come from MET × the latest weight × hours, never typed in.
            var weightKg = await _db.HealthMeasurements
                .Where(m => m.ClientId == clientId)
                .OrderByDescending(m => m.MeasuredAt)
                .Select(m => (decimal?)m.WeightKg)
                .FirstOrDefaultAsync();
            if (weightKg is null)
            {
                throw new BusinessRuleException("BR-14", "Record your weight first, so the calories you burn can be worked out.");
            }

            var log = new WorkoutLog
            {
                ClientId = clientId.Value,
                WorkoutPlanId = plan?.WorkoutPlanId,
                WorkoutDate = date,
                PerceivedEffort = model.PerceivedEffort,
                Notes = model.Notes.NullIfBlank(),
                CreatedAt = _clock.Now
            };

            foreach (var entry in entries)
            {
                log.AddEntry(exercises[entry.ExerciseId!.Value], weightKg.Value, entry.DurationMinutes!.Value,
                    entry.Sets, entry.Reps, entry.LoadKg, entry.DistanceKm);
            }

            _db.WorkoutLogs.Add(log);
            await _db.SaveChangesAsync();

            // The audit row says what happened without health values.
            await _audit.LogAsync(clientUserId, AuditAction.Create, nameof(WorkoutLog), Key(log.WorkoutLogId),
                $"Workout logged with {log.Entries.Count} exercise{(log.Entries.Count == 1 ? string.Empty : "s")}.");
            return log.WorkoutLogId;
        }

        /// <inheritdoc />
        public async Task<PagedResult<WorkoutHistoryItemViewModel>> GetHistoryAsync(int clientId, int page) =>
            await _db.WorkoutLogs.AsNoTracking()
                .Where(l => l.ClientId == clientId)
                .OrderByDescending(l => l.WorkoutDate)
                .ThenByDescending(l => l.WorkoutLogId)
                // Entries and feedback are two collections; separate queries avoid multiplying their rows together.
                .AsSplitQuery()
                .Select(l => new WorkoutHistoryItemViewModel
                {
                    WorkoutLogId = l.WorkoutLogId,
                    WorkoutDate = l.WorkoutDate,
                    PlanTitle = l.WorkoutPlan != null ? l.WorkoutPlan.Title : null,
                    PerceivedEffort = l.PerceivedEffort,
                    Notes = l.Notes,
                    Entries = l.Entries
                        .OrderBy(e => e.WorkoutLogEntryId)
                        .Select(e => new WorkoutEntryViewModel
                        {
                            ExerciseName = e.Exercise.Name,
                            SetsCompleted = e.SetsCompleted,
                            RepsCompleted = e.RepsCompleted,
                            WeightKg = e.WeightKg,
                            DurationMinutes = e.DurationMinutes,
                            DistanceKm = e.DistanceKm,
                            CaloriesBurned = e.CaloriesBurned
                        })
                        .ToList(),
                    Feedback = l.Feedback
                        .OrderBy(f => f.CreatedAt)
                        .Select(f => new FeedbackItemViewModel
                        {
                            FeedbackId = f.FeedbackId,
                            TrainerName = f.Trainer.User.FirstName + " " + f.Trainer.User.LastName,
                            Category = f.Category,
                            Message = f.Message,
                            Rating = f.Rating,
                            CreatedAt = f.CreatedAt,
                            WorkoutDate = l.WorkoutDate
                        })
                        .ToList()
                })
                .ToPagedResultAsync(page);

        // Activation inside the caller's transaction: the entity checks BR-10 and BR-12, then BR-09 completes the
        // client's previous active plan, and the client gets a PlanAssigned notification.
        private async Task ActivateCoreAsync(WorkoutPlan plan, string trainerUserId)
        {
            plan.Activate();
            var now = _clock.Now;

            // BR-09: one active workout plan per client.
            var previous = await _db.WorkoutPlans
                .Where(p => p.ClientId == plan.ClientId && p.Status == PlanStatus.Active && p.WorkoutPlanId != plan.WorkoutPlanId)
                .ToListAsync();
            foreach (var old in previous)
            {
                old.Complete();
                old.UpdatedAt = now;
            }

            plan.UpdatedAt = now;
            await _db.SaveChangesAsync();

            var client = await _db.ClientProfiles.AsNoTracking()
                .Where(c => c.ClientId == plan.ClientId)
                .Select(c => new { c.UserId })
                .SingleAsync();
            var trainerName = await _db.TrainerProfiles.AsNoTracking()
                .Where(t => t.TrainerId == plan.TrainerId)
                .Select(t => t.User.FirstName + " " + t.User.LastName)
                .SingleAsync();

            await _notifications.NotifyAsync(client.UserId, NotificationType.PlanAssigned, "New workout plan",
                $"{trainerName} has given you a new workout plan: {plan.Title}, starting {plan.StartDate:dd MMM yyyy}.",
                "/Client/Workouts/MyPlan");
            await _audit.LogAsync(trainerUserId, AuditAction.Update, nameof(WorkoutPlan), Key(plan.WorkoutPlanId),
                $"Workout plan activated: {plan.Title}" + (previous.Count > 0 ? "; the previous plan was completed." : "."));
        }

        // The trainer's own plan, with its lines; a client plan only while the client is still assigned (BR-08).
        private async Task<WorkoutPlan?> LoadOwnPlanAsync(int planId, string trainerUserId)
        {
            if (await TrainerIdAsync(trainerUserId) is not { } trainerId)
            {
                return null;
            }

            var plan = await _db.WorkoutPlans
                .Include(p => p.Exercises).ThenInclude(e => e.Exercise)
                .Include(p => p.Client).ThenInclude(c => c!.User)
                .FirstOrDefaultAsync(p => p.WorkoutPlanId == planId && p.TrainerId == trainerId);
            if (plan is null)
            {
                return null;
            }

            if (plan.ClientId is { } clientId && !await _assignments.IsTrainerAssignedToClientAsync(trainerId, clientId))
            {
                return null;
            }

            return plan;
        }

        private async Task FillEditDisplayAsync(WorkoutPlanEditViewModel model, WorkoutPlan plan)
        {
            model.WorkoutPlanId = plan.WorkoutPlanId;
            model.IsTemplate = plan.IsTemplate;
            model.Status = plan.Status;
            model.ClientName = plan.Client?.User.FullName;
            model.TimesAssigned = plan.IsTemplate
                ? await _db.WorkoutPlans.CountAsync(p => p.SourceTemplateId == plan.WorkoutPlanId)
                : 0;

            // Posted lines lose their display-only values; take them from the database by key.
            var lines = plan.Exercises.ToDictionary(l => l.WorkoutPlanExerciseId);
            model.Lines = model.Lines.Where(l => lines.ContainsKey(l.WorkoutPlanExerciseId)).ToList();
            foreach (var line in model.Lines)
            {
                var stored = lines[line.WorkoutPlanExerciseId];
                line.ExerciseName = stored.Exercise.Name;
                line.DayOfWeek = stored.DayOfWeek;
                line.SortOrder = stored.SortOrder;
            }

            model.Lines = model.Lines.OrderBy(l => MondayFirst(l.DayOfWeek)).ThenBy(l => l.SortOrder).ToList();
            model.ExerciseOptions = (await _db.Exercises.AsNoTracking()
                    .Where(e => e.IsActive)
                    .OrderBy(e => e.Category)
                    .ThenBy(e => e.Name)
                    .ToListAsync())
                .Select(e => new SelectOptionViewModel(e.ExerciseId, e.Name, e.Category.GetDisplayName()))
                .ToList();
        }

        private async Task<IReadOnlyList<SelectOptionViewModel>> ClientOptionsAsync(string trainerUserId) =>
            (await _assignments.GetTrainerClientsAsync(trainerUserId))
            .Select(c => new SelectOptionViewModel(c.ClientId, $"{c.FullName} ({c.FitnessGoal.GetDisplayName()})"))
            .ToList();

        private IQueryable<WorkoutPlan> ActivePlanQuery(int clientId) =>
            _db.WorkoutPlans.AsNoTracking()
                .Include(p => p.Exercises).ThenInclude(e => e.Exercise)
                .Where(p => p.ClientId == clientId && p.Status == PlanStatus.Active)
                .OrderByDescending(p => p.StartDate);

        // The plan's week, Monday first; with a Monday, each day also gets its date and whether a workout was logged.
        private IReadOnlyList<PlanDayViewModel> WeekOf(WorkoutPlan plan, DateTime? monday = null, ISet<DateTime>? logged = null) => WorkoutText.WeekFromMonday
            .Select((day, index) => new PlanDayViewModel
            {
                DayOfWeek = day,
                IsToday = day == _clock.Today.DayOfWeek,
                Date = monday?.AddDays(index),
                IsLogged = monday is { } first && logged is not null && logged.Contains(first.AddDays(index)),
                Lines = plan.GetExercisesFor(day).Select(l => new PlanLineViewModel
                {
                    ExerciseName = l.Exercise.Name,
                    Category = l.Exercise.Category,
                    Equipment = l.Exercise.Equipment,
                    Sets = l.Sets,
                    Reps = l.Reps,
                    DurationMinutes = l.DurationMinutes,
                    RestSeconds = l.RestSeconds,
                    Notes = l.Notes
                }).ToList()
            })
            .ToList();

        private Task<int?> TrainerIdAsync(string trainerUserId) =>
            _db.TrainerProfiles.Where(t => t.UserId == trainerUserId).Select(t => (int?)t.TrainerId).FirstOrDefaultAsync();

        // BR-21: completed and archived plans are history and stay as they were.
        private static void EnsureEditable(WorkoutPlan plan)
        {
            if (plan.Status is PlanStatus.Completed or PlanStatus.Archived)
            {
                throw new BusinessRuleException("BR-21", "Completed and archived plans are kept as history and can't be changed.");
            }
        }

        // BR-10: a client plan has a start date, and the end date is on or after it.
        private static void EnsureValidDates(WorkoutPlan plan)
        {
            if (plan.IsTemplate)
            {
                return;
            }

            if (plan.StartDate is null)
            {
                throw new BusinessRuleException("BR-10", "A client plan needs a start date.");
            }

            if (plan.EndDate < plan.StartDate)
            {
                throw new BusinessRuleException("BR-10", "The end date can't be before the start date.");
            }
        }

        private static IEnumerable<WorkoutPlanExercise> OrderLines(IEnumerable<WorkoutPlanExercise> lines) =>
            lines.OrderBy(l => MondayFirst(l.DayOfWeek)).ThenBy(l => l.SortOrder);

        // System.DayOfWeek starts on Sunday; the app's weeks start on Monday.
        private static int MondayFirst(DayOfWeek day) => ((int)day + 6) % 7;

        private static string Key(int id) => id.ToString(CultureInfo.InvariantCulture);
    }
}
