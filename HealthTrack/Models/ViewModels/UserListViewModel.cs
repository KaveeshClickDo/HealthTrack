namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// The user list page: the filters and one page of users.
    /// </summary>
    public class UserListViewModel
    {
        /// <summary>The filters in use.</summary>
        public UserFilterViewModel Filter { get; init; } = new();

        /// <summary>The page of users.</summary>
        public PagedResult<UserListItemViewModel> Results { get; init; } = new([], 1, 20, 0);
    }
}
