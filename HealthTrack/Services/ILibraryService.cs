using HealthTrack.Models.Entities;
using HealthTrack.Models.Enums;
using HealthTrack.Models.ViewModels;

namespace HealthTrack.Services
{
    /// <summary>
    /// The exercise and food libraries (UC32). Entries are deactivated, never deleted (BR-21).
    /// </summary>
    public interface ILibraryService
    {
        /// <summary>
        /// One page of exercises by name.
        /// </summary>
        /// <param name="search">Part of the name.</param>
        /// <param name="category">Only this category.</param>
        /// <param name="includeInactive">Also return deactivated exercises.</param>
        /// <param name="page">The 1-based page.</param>
        /// <returns>The page.</returns>
        Task<PagedResult<Exercise>> GetExercisesAsync(string? search, ExerciseCategory? category, bool includeInactive, int page);

        /// <summary>
        /// One exercise as an edit form.
        /// </summary>
        /// <param name="exerciseId">The exercise.</param>
        /// <returns>The form, or null when the exercise doesn't exist.</returns>
        Task<ExerciseViewModel?> GetExerciseAsync(int exerciseId);

        /// <summary>
        /// Adds an exercise (no ID) or updates one. Names are unique, ignoring case, inactive entries included.
        /// </summary>
        /// <param name="model">The validated form.</param>
        /// <param name="actingAdminId">The administrator making the change.</param>
        /// <returns>The exercise's ID, or null when the exercise to update doesn't exist.</returns>
        /// <exception cref="Helpers.BusinessRuleException">The name is taken.</exception>
        Task<int?> SaveExerciseAsync(ExerciseViewModel model, string actingAdminId);

        /// <summary>
        /// Activates or deactivates an exercise. Deactivated exercises disappear from the pickers but stay in history (BR-21).
        /// </summary>
        /// <param name="exerciseId">The exercise.</param>
        /// <param name="isActive">The new state.</param>
        /// <param name="actingAdminId">The administrator making the change.</param>
        /// <returns>False when the exercise doesn't exist.</returns>
        Task<bool> SetExerciseActiveAsync(int exerciseId, bool isActive, string actingAdminId);

        /// <summary>
        /// One page of foods by name.
        /// </summary>
        /// <param name="search">Part of the name.</param>
        /// <param name="category">Only this category.</param>
        /// <param name="includeInactive">Also return deactivated foods.</param>
        /// <param name="page">The 1-based page.</param>
        /// <returns>The page.</returns>
        Task<PagedResult<FoodItem>> GetFoodsAsync(string? search, FoodCategory? category, bool includeInactive, int page);

        /// <summary>
        /// One food as an edit form.
        /// </summary>
        /// <param name="foodItemId">The food.</param>
        /// <returns>The form, or null when the food doesn't exist.</returns>
        Task<FoodItemViewModel?> GetFoodAsync(int foodItemId);

        /// <summary>
        /// Adds a food (no ID) or updates one. Names are unique, ignoring case, inactive entries included.
        /// </summary>
        /// <param name="model">The validated form.</param>
        /// <param name="actingAdminId">The administrator making the change.</param>
        /// <returns>The food's ID, or null when the food to update doesn't exist.</returns>
        /// <exception cref="Helpers.BusinessRuleException">The name is taken.</exception>
        Task<int?> SaveFoodAsync(FoodItemViewModel model, string actingAdminId);

        /// <summary>
        /// Activates or deactivates a food. Deactivated foods disappear from the pickers but stay in history (BR-21).
        /// </summary>
        /// <param name="foodItemId">The food.</param>
        /// <param name="isActive">The new state.</param>
        /// <param name="actingAdminId">The administrator making the change.</param>
        /// <returns>False when the food doesn't exist.</returns>
        Task<bool> SetFoodActiveAsync(int foodItemId, bool isActive, string actingAdminId);

        /// <summary>
        /// Every active exercise by category and name, for the plan and log pickers.
        /// </summary>
        /// <returns>The exercises.</returns>
        Task<IReadOnlyList<Exercise>> GetActiveExercisesAsync();

        /// <summary>
        /// Every active food by category and name, for the meal log picker.
        /// </summary>
        /// <returns>The foods.</returns>
        Task<IReadOnlyList<FoodItem>> GetActiveFoodsAsync();
    }
}
