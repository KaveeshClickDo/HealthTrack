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
    /// Diet plans and meal logging.
    /// </summary>
    public class DietService : IDietService
    {
        // The upper end of a plan's daily calorie target (spec 2.6).
        private const int MaximumCalorieTarget = 5000;

        private readonly ApplicationDbContext _db;
        private readonly IAssignmentService _assignments;
        private readonly IProgressService _progress;
        private readonly INotificationService _notifications;
        private readonly IAuditService _audit;
        private readonly IClock _clock;

        /// <summary>
        /// Creates the service.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="assignments">Checks that a trainer may reach a client (BR-08).</param>
        /// <param name="progress">Suggests the calorie target from the client's TDEE and goal.</param>
        /// <param name="notifications">Tells clients about new plans.</param>
        /// <param name="audit">Records plan changes and meal logs.</param>
        /// <param name="clock">Supplies the time.</param>
        public DietService(ApplicationDbContext db, IAssignmentService assignments, IProgressService progress,
            INotificationService notifications, IAuditService audit, IClock clock)
        {
            _db = db;
            _assignments = assignments;
            _progress = progress;
            _notifications = notifications;
            _audit = audit;
            _clock = clock;
        }

        /// <inheritdoc />
        public async Task<DietPlanViewModel?> GetCreatePlanFormAsync(string trainerUserId, int? clientId)
        {
            var model = new DietPlanViewModel { ClientId = clientId, StartDate = _clock.Today };
            await FillPlanOptionsAsync(model, trainerUserId);

            if (clientId is not null)
            {
                // BR-08: only an assigned client; FillPlanOptionsAsync names the client only when they are in the list.
                if (model.ClientName is null)
                {
                    return null;
                }

                // Spec 1.10: pre-filled from the client's TDEE and goal, kept inside the plan's 1,200–5,000 kcal range.
                model.DailyCalorieTarget = model.SuggestedCalorieTarget is { } suggested
                    ? Math.Clamp(suggested, HealthCalculator.MinimumCalorieTarget, MaximumCalorieTarget)
                    : null;
            }

            return model;
        }

        /// <inheritdoc />
        public async Task FillPlanOptionsAsync(DietPlanViewModel model, string trainerUserId)
        {
            var clients = await _assignments.GetTrainerClientsAsync(trainerUserId);
            model.ClientOptions = clients
                .Select(c => new SelectOptionViewModel(c.ClientId, $"{c.FullName} ({c.FitnessGoal.GetDisplayName()})"))
                .ToList();

            var chosen = clients.FirstOrDefault(c => c.ClientId == model.ClientId);
            model.ClientName = chosen?.FullName;
            model.SuggestedCalorieTarget = chosen is null ? null : await _progress.SuggestCalorieTargetAsync(chosen.ClientId);
        }

        /// <inheritdoc />
        public async Task<int?> CreatePlanAsync(string trainerUserId, DietPlanViewModel model)
        {
            if (await TrainerIdAsync(trainerUserId) is not { } trainerId)
            {
                return null;
            }

            // BR-08: trainers plan only for their assigned clients.
            if (model.ClientId is not { } clientId || !await _assignments.IsTrainerAssignedToClientAsync(trainerId, clientId))
            {
                return null;
            }

            var plan = new DietPlan
            {
                TrainerId = trainerId,
                ClientId = clientId,
                Title = model.Title.Trim(),
                Description = model.Description.NullIfBlank(),
                DailyCalorieTarget = model.DailyCalorieTarget!.Value,
                ProteinTargetG = model.ProteinTargetG,
                CarbsTargetG = model.CarbsTargetG,
                FatTargetG = model.FatTargetG,
                StartDate = model.StartDate!.Value.Date,
                EndDate = model.EndDate?.Date,
                Status = PlanStatus.Draft,
                CreatedAt = _clock.Now
            };
            EnsureValidDates(plan);

            _db.DietPlans.Add(plan);
            await _db.SaveChangesAsync();
            await _audit.LogAsync(trainerUserId, AuditAction.Create, nameof(DietPlan), Key(plan.DietPlanId),
                $"Diet plan created: {plan.Title}.");
            return plan.DietPlanId;
        }

        /// <inheritdoc />
        public async Task<PagedResult<DietPlanListItemViewModel>> GetTrainerPlansAsync(string trainerUserId, int page)
        {
            var trainerId = await TrainerIdAsync(trainerUserId) ?? 0;

            return await _db.DietPlans.AsNoTracking()
                .Where(p => p.TrainerId == trainerId)
                .OrderBy(p => p.Status == PlanStatus.Archived ? 2 : p.Status == PlanStatus.Completed ? 1 : 0)
                .ThenByDescending(p => p.CreatedAt)
                .Select(p => new DietPlanListItemViewModel
                {
                    DietPlanId = p.DietPlanId,
                    Title = p.Title,
                    ClientName = p.Client.User.FirstName + " " + p.Client.User.LastName,
                    Status = p.Status,
                    DailyCalorieTarget = p.DailyCalorieTarget,
                    StartDate = p.StartDate,
                    EndDate = p.EndDate,
                    MealCount = p.Meals.Count,
                    ClientId = p.ClientId,
                    ProteinTargetG = p.ProteinTargetG,
                    CarbsTargetG = p.CarbsTargetG,
                    FatTargetG = p.FatTargetG
                })
                .ToPagedResultAsync(page);
        }

        /// <inheritdoc />
        public async Task<DietPlanEditViewModel?> GetPlanForEditAsync(int planId, string trainerUserId)
        {
            var plan = await LoadOwnPlanAsync(planId, trainerUserId);
            if (plan is null)
            {
                return null;
            }

            var model = new DietPlanEditViewModel
            {
                Title = plan.Title,
                Description = plan.Description,
                DailyCalorieTarget = plan.DailyCalorieTarget,
                ProteinTargetG = plan.ProteinTargetG,
                CarbsTargetG = plan.CarbsTargetG,
                FatTargetG = plan.FatTargetG,
                StartDate = plan.StartDate,
                EndDate = plan.EndDate,
                Meals = plan.Meals.Select(m => new PlanMealEditViewModel
                {
                    DietPlanMealId = m.DietPlanMealId,
                    ScheduledTime = m.ScheduledTime,
                    MealDescription = m.MealDescription,
                    TargetCalories = m.TargetCalories
                }).ToList()
            };
            await FillEditDisplayAsync(model, plan);
            return model;
        }

        /// <inheritdoc />
        public async Task<bool> FillEditDisplayAsync(DietPlanEditViewModel model, int planId, string trainerUserId)
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
        public async Task<bool> UpdatePlanAsync(int planId, string trainerUserId, DietPlanEditViewModel model)
        {
            var plan = await LoadOwnPlanAsync(planId, trainerUserId);
            if (plan is null)
            {
                return false;
            }

            EnsureEditable(plan);

            plan.Title = model.Title.Trim();
            plan.Description = model.Description.NullIfBlank();
            plan.DailyCalorieTarget = model.DailyCalorieTarget!.Value;
            plan.ProteinTargetG = model.ProteinTargetG;
            plan.CarbsTargetG = model.CarbsTargetG;
            plan.FatTargetG = model.FatTargetG;
            plan.StartDate = model.StartDate!.Value.Date;
            plan.EndDate = model.EndDate?.Date;
            EnsureValidDates(plan);

            var loggedMealIds = await LoggedMealIdsAsync(plan);

            // Meals are matched by key; keys from another plan are ignored.
            foreach (var posted in model.Meals)
            {
                var meal = plan.Meals.FirstOrDefault(m => m.DietPlanMealId == posted.DietPlanMealId);
                if (meal is null)
                {
                    continue;
                }

                if (posted.Remove)
                {
                    // BR-21: a meal the client has logged is history; the logs point at it.
                    if (loggedMealIds.Contains(meal.DietPlanMealId))
                    {
                        throw new BusinessRuleException("BR-21",
                            $"The {meal.MealType.GetDisplayName().ToLowerInvariant()} at {meal.ScheduledTime:hh\\:mm} has already been logged, so it can't be removed. Change it instead.");
                    }

                    _db.DietPlanMeals.Remove(meal);
                    continue;
                }

                meal.ScheduledTime = posted.ScheduledTime!.Value;
                meal.MealDescription = posted.MealDescription.Trim();
                meal.TargetCalories = posted.TargetCalories!.Value;
            }

            // BR-10: an active plan must keep at least one meal.
            if (plan.Status == PlanStatus.Active && plan.Meals.All(m => _db.Entry(m).State == EntityState.Deleted))
            {
                throw new BusinessRuleException("BR-10", "An active plan needs at least one meal.");
            }

            plan.UpdatedAt = _clock.Now;
            await _db.SaveChangesAsync();
            await _audit.LogAsync(trainerUserId, AuditAction.Update, nameof(DietPlan), Key(plan.DietPlanId),
                $"Diet plan updated: {plan.Title}.");
            return true;
        }

        /// <inheritdoc />
        public async Task<bool> AddMealAsync(int planId, string trainerUserId, PlanMealViewModel model)
        {
            var plan = await LoadOwnPlanAsync(planId, trainerUserId);
            if (plan is null)
            {
                return false;
            }

            EnsureEditable(plan);

            plan.Meals.Add(new DietPlanMeal
            {
                DayOfWeek = model.DayOfWeek,
                MealType = model.MealType!.Value,
                ScheduledTime = model.ScheduledTime!.Value,
                MealDescription = model.MealDescription.Trim(),
                TargetCalories = model.TargetCalories!.Value
            });
            plan.UpdatedAt = _clock.Now;
            await _db.SaveChangesAsync();
            await _audit.LogAsync(trainerUserId, AuditAction.Update, nameof(DietPlan), Key(plan.DietPlanId),
                $"{model.MealType.Value.GetDisplayName()} added to {plan.Title} for {DayText(model.DayOfWeek)}.");
            return true;
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

            // The entity checks BR-10 (at least one meal) and that the plan is a draft.
            plan.Activate();
            var now = _clock.Now;

            // BR-09: one active diet plan per client.
            var previous = await _db.DietPlans
                .Where(p => p.ClientId == plan.ClientId && p.Status == PlanStatus.Active && p.DietPlanId != plan.DietPlanId)
                .ToListAsync();
            foreach (var old in previous)
            {
                old.Complete();
                old.UpdatedAt = now;
            }

            plan.UpdatedAt = now;
            await _db.SaveChangesAsync();

            var trainerName = await _db.TrainerProfiles.AsNoTracking()
                .Where(t => t.TrainerId == plan.TrainerId)
                .Select(t => t.User.FirstName + " " + t.User.LastName)
                .SingleAsync();
            await _notifications.NotifyAsync(plan.Client.UserId, NotificationType.PlanAssigned, "New diet plan",
                $"{trainerName} has given you a new diet plan: {plan.Title}, {plan.DailyCalorieTarget:N0} kcal a day from {plan.StartDate:dd MMM yyyy}.",
                "/Client/Diet/MyPlan");
            await _audit.LogAsync(trainerUserId, AuditAction.Update, nameof(DietPlan), Key(plan.DietPlanId),
                $"Diet plan activated: {plan.Title}" + (previous.Count > 0 ? "; the previous plan was completed." : "."));

            await transaction.CommitAsync();
            return true;
        }

        /// <inheritdoc />
        public async Task<DietWeekViewModel> GetWeekAsync(int clientId)
        {
            var plan = await ActivePlanQuery(clientId)
                .Include(p => p.Trainer).ThenInclude(t => t.User)
                .FirstOrDefaultAsync();
            if (plan is null)
            {
                return new DietWeekViewModel { HasPlan = false };
            }

            var today = _clock.Today;
            return new DietWeekViewModel
            {
                HasPlan = true,
                Title = plan.Title,
                Description = plan.Description,
                TrainerName = plan.Trainer.User.FullName,
                StartDate = plan.StartDate,
                EndDate = plan.EndDate,
                HasStarted = plan.StartDate <= today,
                TodaysMeals = await GetTodaysMealsAsync(clientId, today),
                Today = await GetDailySummaryAsync(clientId, today),
                Days = WorkoutText.WeekFromMonday.Select(day => new DietDayViewModel
                {
                    DayOfWeek = day,
                    IsToday = day == today.DayOfWeek,
                    Meals = plan.GetMealsFor(day).Select(m => Planned(m, null)).ToList()
                }).ToList()
            };
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<PlannedMealViewModel>> GetTodaysMealsAsync(int clientId, DateTime today)
        {
            var date = today.Date;
            var plan = await ActivePlanQuery(clientId).FirstOrDefaultAsync();
            if (plan is null || !plan.IsActiveOn(date))
            {
                return [];
            }

            var meals = plan.GetMealsFor(date.DayOfWeek);
            var mealIds = meals.Select(m => m.DietPlanMealId).ToList();
            var logged = (await _db.MealLogs.AsNoTracking()
                    .Where(l => l.ClientId == clientId && l.MealDate == date && l.DietPlanMealId != null && mealIds.Contains(l.DietPlanMealId.Value))
                    .Select(l => new { MealId = l.DietPlanMealId!.Value, Calories = l.Items.Sum(i => i.Calories) })
                    .ToListAsync())
                .GroupBy(l => l.MealId)
                .ToDictionary(g => g.Key, g => g.Sum(l => l.Calories));

            return meals.Select(m => Planned(m, logged.TryGetValue(m.DietPlanMealId, out var kcal) ? kcal : null)).ToList();
        }

        /// <inheritdoc />
        public async Task<LogMealViewModel?> GetLogMealFormAsync(int clientId, int? dietPlanMealId)
        {
            var model = new LogMealViewModel { MealDate = _clock.Today, Items = [new LogMealItemViewModel()] };

            if (dietPlanMealId is { } mealId)
            {
                // Only a meal of the client's own active plan.
                var meal = await _db.DietPlanMeals.AsNoTracking()
                    .FirstOrDefaultAsync(m => m.DietPlanMealId == mealId && m.DietPlan.ClientId == clientId
                        && m.DietPlan.Status == PlanStatus.Active);
                if (meal is null)
                {
                    return null;
                }

                model.DietPlanMealId = meal.DietPlanMealId;
                model.MealType = meal.MealType;
            }

            await FillLogMealOptionsAsync(model, clientId);
            return model;
        }

        /// <inheritdoc />
        public async Task FillLogMealOptionsAsync(LogMealViewModel model, int clientId)
        {
            if (model.DietPlanMealId is { } mealId)
            {
                var meal = await _db.DietPlanMeals.AsNoTracking()
                    .FirstOrDefaultAsync(m => m.DietPlanMealId == mealId && m.DietPlan.ClientId == clientId);
                model.PlannedDescription = meal?.MealDescription;
                model.PlannedCalories = meal?.TargetCalories;
            }

            // BR-21: only active foods can be picked.
            model.FoodOptions = (await _db.FoodItems.AsNoTracking()
                    .Where(f => f.IsActive)
                    .OrderBy(f => f.Category)
                    .ThenBy(f => f.Name)
                    .ToListAsync())
                .Select(f => new FoodOptionViewModel
                {
                    FoodItemId = f.FoodItemId,
                    Name = f.Name,
                    Category = f.Category.GetDisplayName(),
                    Serving = $"{f.ServingSize:0.##} {f.ServingUnit}",
                    CaloriesPerServing = f.CaloriesPerServing
                })
                .ToList();
        }

        /// <inheritdoc />
        public async Task<int?> LogMealAsync(string clientUserId, LogMealViewModel model)
        {
            var clientId = await _db.ClientProfiles.Where(c => c.UserId == clientUserId).Select(c => (int?)c.ClientId).FirstOrDefaultAsync();
            if (clientId is null)
            {
                return null;
            }

            // BR-13: not in the future, and at least one food.
            var date = model.MealDate!.Value.Date;
            if (date > _clock.Today)
            {
                throw new BusinessRuleException("BR-13", "A meal can't be logged for a future date.");
            }

            var items = model.Items.Where(i => i.FoodItemId is not null && i.Quantity is not null).ToList();
            if (items.Count == 0)
            {
                throw new BusinessRuleException("BR-13", LogMealViewModel.NoItemsMessage);
            }

            // A planned meal must come from one of the client's own plans.
            if (model.DietPlanMealId is { } mealId
                && !await _db.DietPlanMeals.AnyAsync(m => m.DietPlanMealId == mealId && m.DietPlan.ClientId == clientId))
            {
                return null;
            }

            var foodIds = items.Select(i => i.FoodItemId!.Value).Distinct().ToList();
            var foods = await _db.FoodItems.AsNoTracking()
                .Where(f => foodIds.Contains(f.FoodItemId))
                .ToDictionaryAsync(f => f.FoodItemId);

            // BR-21: deactivated foods can't be picked for new logs.
            if (foodIds.Any(id => !foods.TryGetValue(id, out var food) || !food.IsActive))
            {
                throw new BusinessRuleException("BR-21", "One of the foods is no longer in the library.");
            }

            var log = new MealLog
            {
                ClientId = clientId.Value,
                DietPlanMealId = model.DietPlanMealId,
                MealDate = date,
                MealType = model.MealType!.Value,
                Notes = model.Notes.NullIfBlank(),
                CreatedAt = _clock.Now
            };

            // UC36, BR-14: each item stores quantity × the food's per-serving values as a snapshot.
            foreach (var item in items)
            {
                log.AddItem(foods[item.FoodItemId!.Value], item.Quantity!.Value);
            }

            _db.MealLogs.Add(log);
            await _db.SaveChangesAsync();

            // The audit row says what happened without health values.
            await _audit.LogAsync(clientUserId, AuditAction.Create, nameof(MealLog), Key(log.MealLogId),
                $"Meal logged with {log.Items.Count} food{(log.Items.Count == 1 ? string.Empty : "s")}.");
            return log.MealLogId;
        }

        /// <inheritdoc />
        public async Task<DailyNutritionViewModel> GetDailySummaryAsync(int clientId, DateTime date) =>
            (await NutritionDays.GetDaysAsync(_db, clientId, [date.Date])).Single();

        /// <inheritdoc />
        public async Task<PagedResult<DailyNutritionViewModel>> GetHistoryAsync(int clientId, int page)
        {
            var dates = await _db.MealLogs.AsNoTracking()
                .Where(m => m.ClientId == clientId)
                .Select(m => m.MealDate)
                .Distinct()
                .OrderByDescending(d => d)
                .ToPagedResultAsync(page);
            var days = await NutritionDays.GetDaysAsync(_db, clientId, dates.Items);
            return new PagedResult<DailyNutritionViewModel>(days, dates.Page, dates.PageSize, dates.TotalCount);
        }

        // The trainer's own plan, with its meals; only while the client is still assigned (BR-08).
        private async Task<DietPlan?> LoadOwnPlanAsync(int planId, string trainerUserId)
        {
            if (await TrainerIdAsync(trainerUserId) is not { } trainerId)
            {
                return null;
            }

            var plan = await _db.DietPlans
                .Include(p => p.Meals)
                .Include(p => p.Client).ThenInclude(c => c.User)
                .FirstOrDefaultAsync(p => p.DietPlanId == planId && p.TrainerId == trainerId);
            if (plan is null || !await _assignments.IsTrainerAssignedToClientAsync(trainerId, plan.ClientId))
            {
                return null;
            }

            return plan;
        }

        private async Task FillEditDisplayAsync(DietPlanEditViewModel model, DietPlan plan)
        {
            model.DietPlanId = plan.DietPlanId;
            model.Status = plan.Status;
            model.ClientName = plan.Client.User.FullName;
            model.SuggestedCalorieTarget = await _progress.SuggestCalorieTargetAsync(plan.ClientId);

            // Posted meals lose their display-only values; take them from the database by key.
            var meals = plan.Meals.ToDictionary(m => m.DietPlanMealId);
            var timesLogged = (await _db.MealLogs.AsNoTracking()
                    .Where(l => l.DietPlanMealId != null && l.DietPlanMeal!.DietPlanId == plan.DietPlanId)
                    .Select(l => l.DietPlanMealId!.Value)
                    .ToListAsync())
                .GroupBy(id => id)
                .ToDictionary(g => g.Key, g => g.Count());

            model.Meals = model.Meals.Where(m => meals.ContainsKey(m.DietPlanMealId)).ToList();
            foreach (var meal in model.Meals)
            {
                var stored = meals[meal.DietPlanMealId];
                meal.DayOfWeek = stored.DayOfWeek;
                meal.MealType = stored.MealType;
                meal.TimesLogged = timesLogged.GetValueOrDefault(meal.DietPlanMealId);
            }

            // Every-day meals first, then Monday to Sunday; by time within a day.
            model.Meals = model.Meals
                .OrderBy(m => m.DayOfWeek is { } day ? MondayFirst(day) + 1 : 0)
                .ThenBy(m => meals[m.DietPlanMealId].ScheduledTime)
                .ToList();
        }

        private async Task<HashSet<int>> LoggedMealIdsAsync(DietPlan plan)
        {
            var mealIds = plan.Meals.Select(m => m.DietPlanMealId).ToList();
            return (await _db.MealLogs.AsNoTracking()
                    .Where(l => l.DietPlanMealId != null && mealIds.Contains(l.DietPlanMealId.Value))
                    .Select(l => l.DietPlanMealId!.Value)
                    .Distinct()
                    .ToListAsync())
                .ToHashSet();
        }

        private IQueryable<DietPlan> ActivePlanQuery(int clientId) =>
            _db.DietPlans.AsNoTracking()
                .Include(p => p.Meals)
                .Where(p => p.ClientId == clientId && p.Status == PlanStatus.Active)
                .OrderByDescending(p => p.StartDate);

        private Task<int?> TrainerIdAsync(string trainerUserId) =>
            _db.TrainerProfiles.Where(t => t.UserId == trainerUserId).Select(t => (int?)t.TrainerId).FirstOrDefaultAsync();

        private static PlannedMealViewModel Planned(DietPlanMeal meal, decimal? loggedCalories) => new()
        {
            DietPlanMealId = meal.DietPlanMealId,
            DayOfWeek = meal.DayOfWeek,
            MealType = meal.MealType,
            ScheduledTime = meal.ScheduledTime,
            MealDescription = meal.MealDescription,
            TargetCalories = meal.TargetCalories,
            IsLogged = loggedCalories is not null,
            LoggedCalories = loggedCalories
        };

        // BR-21: completed and archived plans are history and stay as they were.
        private static void EnsureEditable(DietPlan plan)
        {
            if (plan.Status is PlanStatus.Completed or PlanStatus.Archived)
            {
                throw new BusinessRuleException("BR-21", "Completed plans are kept as history and can't be changed.");
            }
        }

        // BR-10: the end date is on or after the start date.
        private static void EnsureValidDates(DietPlan plan)
        {
            if (plan.EndDate < plan.StartDate)
            {
                throw new BusinessRuleException("BR-10", "The end date can't be before the start date.");
            }
        }

        private static string DayText(DayOfWeek? day) => day is { } d ? d.ToString() + "s" : "every day";

        // System.DayOfWeek starts on Sunday; the app's weeks start on Monday.
        private static int MondayFirst(DayOfWeek day) => ((int)day + 6) % 7;

        private static string Key(int id) => id.ToString(CultureInfo.InvariantCulture);
    }
}
