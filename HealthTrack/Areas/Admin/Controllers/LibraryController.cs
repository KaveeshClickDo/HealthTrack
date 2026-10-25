using HealthTrack.Helpers;
using HealthTrack.Models.Entities;
using HealthTrack.Models.Enums;
using HealthTrack.Models.ViewModels;
using HealthTrack.Services;
using Microsoft.AspNetCore.Mvc;

namespace HealthTrack.Areas.Admin.Controllers
{
    /// <summary>
    /// Maintain the exercise and food libraries (UC32). Entries are deactivated, never deleted (BR-21).
    /// </summary>
    public class LibraryController : AdminAreaController
    {
        private readonly ILibraryService _library;

        /// <summary>
        /// Creates the controller.
        /// </summary>
        /// <param name="library">The library service.</param>
        public LibraryController(ILibraryService library)
        {
            _library = library;
        }

        /// <summary>
        /// The exercise library, 20 per page.
        /// </summary>
        /// <param name="filter">The filters from the query string.</param>
        /// <param name="page">The page number.</param>
        /// <returns>The list.</returns>
        [HttpGet]
        public async Task<IActionResult> Exercises(LibraryFilterViewModel<ExerciseCategory> filter, int page = 1) =>
            View(new LibraryListViewModel<Exercise, ExerciseCategory>
            {
                Filter = filter,
                Results = await _library.GetExercisesAsync(filter.Search, filter.Category, filter.IncludeInactive, page)
            });

        /// <summary>
        /// The exercise form: a new exercise without an ID, otherwise the exercise to edit.
        /// </summary>
        /// <param name="id">The exercise, or null for a new one.</param>
        /// <returns>The form, or 404.</returns>
        [HttpGet]
        public async Task<IActionResult> EditExercise(int? id)
        {
            if (id is null)
            {
                return View(new ExerciseViewModel());
            }

            var model = await _library.GetExerciseAsync(id.Value);
            return model is null ? NotFound() : View(model);
        }

        /// <summary>
        /// Saves the exercise.
        /// </summary>
        /// <param name="model">The form.</param>
        /// <returns>The exercise list, or the form with its errors.</returns>
        [HttpPost]
        public async Task<IActionResult> EditExercise(ExerciseViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                if (await _library.SaveExerciseAsync(model, CurrentUserId) is null)
                {
                    return NotFound();
                }
            }
            catch (BusinessRuleException ex)
            {
                ModelState.AddModelError(nameof(ExerciseViewModel.Name), ex.Message);
                return View(model);
            }

            SetSuccess($"“{model.Name.Trim()}” has been saved.");
            return RedirectToAction(nameof(Exercises));
        }

        /// <summary>
        /// The food library, 20 per page.
        /// </summary>
        /// <param name="filter">The filters from the query string.</param>
        /// <param name="page">The page number.</param>
        /// <returns>The list.</returns>
        [HttpGet]
        public async Task<IActionResult> Foods(LibraryFilterViewModel<FoodCategory> filter, int page = 1) =>
            View(new LibraryListViewModel<FoodItem, FoodCategory>
            {
                Filter = filter,
                Results = await _library.GetFoodsAsync(filter.Search, filter.Category, filter.IncludeInactive, page)
            });

        /// <summary>
        /// The food form: a new food without an ID, otherwise the food to edit.
        /// </summary>
        /// <param name="id">The food, or null for a new one.</param>
        /// <returns>The form, or 404.</returns>
        [HttpGet]
        public async Task<IActionResult> EditFood(int? id)
        {
            if (id is null)
            {
                return View(new FoodItemViewModel());
            }

            var model = await _library.GetFoodAsync(id.Value);
            return model is null ? NotFound() : View(model);
        }

        /// <summary>
        /// Saves the food.
        /// </summary>
        /// <param name="model">The form.</param>
        /// <returns>The food list, or the form with its errors.</returns>
        [HttpPost]
        public async Task<IActionResult> EditFood(FoodItemViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                if (await _library.SaveFoodAsync(model, CurrentUserId) is null)
                {
                    return NotFound();
                }
            }
            catch (BusinessRuleException ex)
            {
                ModelState.AddModelError(nameof(FoodItemViewModel.Name), ex.Message);
                return View(model);
            }

            SetSuccess($"“{model.Name.Trim()}” has been saved.");
            return RedirectToAction(nameof(Foods));
        }

        /// <summary>
        /// Activates or deactivates an exercise or a food.
        /// </summary>
        /// <param name="type">"exercise" or "food".</param>
        /// <param name="id">The entry's key.</param>
        /// <param name="isActive">The new state.</param>
        /// <param name="returnUrl">The list page to go back to, with its filters.</param>
        /// <returns>The list, or 404.</returns>
        [HttpPost]
        public async Task<IActionResult> ToggleActive(string type, int id, bool isActive, string? returnUrl)
        {
            var found = type switch
            {
                "exercise" => await _library.SetExerciseActiveAsync(id, isActive, CurrentUserId),
                "food" => await _library.SetFoodActiveAsync(id, isActive, CurrentUserId),
                _ => false
            };
            if (!found)
            {
                return NotFound();
            }

            SetSuccess(isActive ? "The entry has been activated." : "The entry has been deactivated. It no longer appears in the pickers.");

            // Only local return URLs are followed, so the form can't be used as an open redirect.
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return RedirectToAction(type == "exercise" ? nameof(Exercises) : nameof(Foods));
        }
    }
}
