namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// A library list page: the filters and one page of entries.
    /// </summary>
    /// <typeparam name="TItem">Exercise or FoodItem.</typeparam>
    /// <typeparam name="TCategory">ExerciseCategory or FoodCategory.</typeparam>
    public class LibraryListViewModel<TItem, TCategory> where TCategory : struct, Enum
    {
        /// <summary>The filters in use.</summary>
        public LibraryFilterViewModel<TCategory> Filter { get; init; } = new();

        /// <summary>The page of entries.</summary>
        public PagedResult<TItem> Results { get; init; } = new([], 1, 20, 0);
    }
}
