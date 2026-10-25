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
    /// Maintains the exercise and food libraries. Nothing is ever deleted (BR-21): old logs and plans keep pointing at
    /// entries, so they are deactivated instead.
    /// </summary>
    public class LibraryService : ILibraryService
    {
        private readonly ApplicationDbContext _db;
        private readonly IAuditService _audit;

        /// <summary>
        /// Creates the service.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="audit">Records library changes.</param>
        public LibraryService(ApplicationDbContext db, IAuditService audit)
        {
            _db = db;
            _audit = audit;
        }

        /// <inheritdoc />
        public async Task<PagedResult<Exercise>> GetExercisesAsync(string? search, ExerciseCategory? category, bool includeInactive, int page)
        {
            var query = _db.Exercises.AsNoTracking();

            if (!includeInactive)
            {
                query = query.Where(e => e.IsActive);
            }

            if (category is { } wanted)
            {
                query = query.Where(e => e.Category == wanted);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToUpperInvariant();
                query = query.Where(e => e.Name.ToUpper().Contains(term));
            }

            return await query.OrderBy(e => e.Name).ToPagedResultAsync(page);
        }

        /// <inheritdoc />
        public async Task<ExerciseViewModel?> GetExerciseAsync(int exerciseId) =>
            await _db.Exercises.AsNoTracking()
                .Where(e => e.ExerciseId == exerciseId)
                .Select(e => new ExerciseViewModel
                {
                    ExerciseId = e.ExerciseId,
                    Name = e.Name,
                    Category = e.Category,
                    MuscleGroup = e.MuscleGroup,
                    Equipment = e.Equipment,
                    Instructions = e.Instructions,
                    MetValue = e.MetValue,
                    IsActive = e.IsActive
                })
                .FirstOrDefaultAsync();

        /// <inheritdoc />
        public async Task<int?> SaveExerciseAsync(ExerciseViewModel model, string actingAdminId)
        {
            var name = model.Name.Trim();
            var upperName = name.ToUpperInvariant();
            var existingId = model.ExerciseId ?? 0;

            // The database has a unique index too, but the service checks first for a friendly message (and for InMemory).
            var duplicate = await _db.Exercises.AsNoTracking()
                .Where(e => e.ExerciseId != existingId && e.Name.ToUpper() == upperName)
                .Select(e => new { e.IsActive })
                .FirstOrDefaultAsync();
            if (duplicate is not null)
            {
                throw new BusinessRuleException("BR-21", DuplicateMessage("an exercise", name, duplicate.IsActive));
            }

            Exercise? exercise;
            if (model.ExerciseId is { } id)
            {
                exercise = await _db.Exercises.FirstOrDefaultAsync(e => e.ExerciseId == id);
                if (exercise is null)
                {
                    return null;
                }
            }
            else
            {
                exercise = new Exercise();
                _db.Exercises.Add(exercise);
            }

            exercise.Name = name;
            exercise.Category = model.Category!.Value;
            exercise.MuscleGroup = model.MuscleGroup!.Value;
            exercise.Equipment = model.Equipment.NullIfBlank();
            exercise.Instructions = model.Instructions.NullIfBlank();
            exercise.MetValue = model.MetValue!.Value;
            await _db.SaveChangesAsync();

            await _audit.LogAsync(actingAdminId, model.ExerciseId is null ? AuditAction.Create : AuditAction.Update, nameof(Exercise),
                exercise.ExerciseId.ToString(CultureInfo.InvariantCulture),
                $"Exercise {(model.ExerciseId is null ? "added" : "updated")}: {name}.");
            return exercise.ExerciseId;
        }

        /// <inheritdoc />
        public async Task<bool> SetExerciseActiveAsync(int exerciseId, bool isActive, string actingAdminId)
        {
            var exercise = await _db.Exercises.FirstOrDefaultAsync(e => e.ExerciseId == exerciseId);
            if (exercise is null)
            {
                return false;
            }

            if (exercise.IsActive != isActive)
            {
                exercise.IsActive = isActive;
                await _db.SaveChangesAsync();
                await _audit.LogAsync(actingAdminId, AuditAction.Update, nameof(Exercise),
                    exercise.ExerciseId.ToString(CultureInfo.InvariantCulture),
                    $"Exercise {(isActive ? "activated" : "deactivated")}: {exercise.Name}.");
            }

            return true;
        }

        /// <inheritdoc />
        public async Task<PagedResult<FoodItem>> GetFoodsAsync(string? search, FoodCategory? category, bool includeInactive, int page)
        {
            var query = _db.FoodItems.AsNoTracking();

            if (!includeInactive)
            {
                query = query.Where(f => f.IsActive);
            }

            if (category is { } wanted)
            {
                query = query.Where(f => f.Category == wanted);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToUpperInvariant();
                query = query.Where(f => f.Name.ToUpper().Contains(term));
            }

            return await query.OrderBy(f => f.Name).ToPagedResultAsync(page);
        }

        /// <inheritdoc />
        public async Task<FoodItemViewModel?> GetFoodAsync(int foodItemId) =>
            await _db.FoodItems.AsNoTracking()
                .Where(f => f.FoodItemId == foodItemId)
                .Select(f => new FoodItemViewModel
                {
                    FoodItemId = f.FoodItemId,
                    Name = f.Name,
                    Category = f.Category,
                    ServingSize = f.ServingSize,
                    ServingUnit = f.ServingUnit,
                    CaloriesPerServing = f.CaloriesPerServing,
                    ProteinG = f.ProteinG,
                    CarbsG = f.CarbsG,
                    FatG = f.FatG,
                    IsActive = f.IsActive
                })
                .FirstOrDefaultAsync();

        /// <inheritdoc />
        public async Task<int?> SaveFoodAsync(FoodItemViewModel model, string actingAdminId)
        {
            var name = model.Name.Trim();
            var upperName = name.ToUpperInvariant();
            var existingId = model.FoodItemId ?? 0;

            var duplicate = await _db.FoodItems.AsNoTracking()
                .Where(f => f.FoodItemId != existingId && f.Name.ToUpper() == upperName)
                .Select(f => new { f.IsActive })
                .FirstOrDefaultAsync();
            if (duplicate is not null)
            {
                throw new BusinessRuleException("BR-21", DuplicateMessage("a food", name, duplicate.IsActive));
            }

            var unit = model.ServingUnit.Trim().ToLowerInvariant();
            if (!FoodItem.ServingUnits.Contains(unit))
            {
                throw new ArgumentException($"Unknown serving unit '{model.ServingUnit}'.", nameof(model));
            }

            FoodItem? food;
            if (model.FoodItemId is { } id)
            {
                food = await _db.FoodItems.FirstOrDefaultAsync(f => f.FoodItemId == id);
                if (food is null)
                {
                    return null;
                }
            }
            else
            {
                food = new FoodItem();
                _db.FoodItems.Add(food);
            }

            // Changing a food's values only affects new logs: logged meals keep their nutrition snapshots (spec 2.11).
            food.Name = name;
            food.Category = model.Category!.Value;
            food.ServingSize = model.ServingSize!.Value;
            food.ServingUnit = unit;
            food.CaloriesPerServing = model.CaloriesPerServing!.Value;
            food.ProteinG = model.ProteinG!.Value;
            food.CarbsG = model.CarbsG!.Value;
            food.FatG = model.FatG!.Value;
            await _db.SaveChangesAsync();

            await _audit.LogAsync(actingAdminId, model.FoodItemId is null ? AuditAction.Create : AuditAction.Update, nameof(FoodItem),
                food.FoodItemId.ToString(CultureInfo.InvariantCulture),
                $"Food {(model.FoodItemId is null ? "added" : "updated")}: {name}.");
            return food.FoodItemId;
        }

        /// <inheritdoc />
        public async Task<bool> SetFoodActiveAsync(int foodItemId, bool isActive, string actingAdminId)
        {
            var food = await _db.FoodItems.FirstOrDefaultAsync(f => f.FoodItemId == foodItemId);
            if (food is null)
            {
                return false;
            }

            if (food.IsActive != isActive)
            {
                food.IsActive = isActive;
                await _db.SaveChangesAsync();
                await _audit.LogAsync(actingAdminId, AuditAction.Update, nameof(FoodItem),
                    food.FoodItemId.ToString(CultureInfo.InvariantCulture),
                    $"Food {(isActive ? "activated" : "deactivated")}: {food.Name}.");
            }

            return true;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<Exercise>> GetActiveExercisesAsync() =>
            await _db.Exercises.AsNoTracking()
                .Where(e => e.IsActive)
                .OrderBy(e => e.Category)
                .ThenBy(e => e.Name)
                .ToListAsync();

        /// <inheritdoc />
        public async Task<IReadOnlyList<FoodItem>> GetActiveFoodsAsync() =>
            await _db.FoodItems.AsNoTracking()
                .Where(f => f.IsActive)
                .OrderBy(f => f.Category)
                .ThenBy(f => f.Name)
                .ToListAsync();

        // BR-21: entries are never deleted, so a name stays taken by a deactivated entry; point the admin at it.
        private static string DuplicateMessage(string kind, string name, bool existingIsActive) => existingIsActive
            ? $"There is already {kind} called “{name}”."
            : $"There is already {kind} called “{name}”, but it is deactivated. Show inactive entries and reactivate it instead.";
    }
}
