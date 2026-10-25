using HealthTrack.Models.Enums;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// One row of the user list.
    /// </summary>
    public class UserListItemViewModel
    {
        /// <summary>The user's ID.</summary>
        public string UserId { get; init; } = string.Empty;

        /// <summary>First and last name.</summary>
        public string FullName { get; init; } = string.Empty;

        /// <summary>Email address.</summary>
        public string Email { get; init; } = string.Empty;

        /// <summary>The user's role.</summary>
        public string Role { get; init; } = string.Empty;

        /// <summary>Whether the account may sign in.</summary>
        public AccountStatus AccountStatus { get; init; }

        /// <summary>Registration time.</summary>
        public DateTime CreatedAt { get; init; }

        /// <summary>Last successful sign-in.</summary>
        public DateTime? LastLoginAt { get; init; }
    }
}
