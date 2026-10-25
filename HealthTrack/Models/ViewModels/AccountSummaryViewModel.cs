using HealthTrack.Models.Enums;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// A read-only summary of the signed-in user's account, for Manage/Index and the dashboards' greeting.
    /// </summary>
    public class AccountSummaryViewModel
    {
        /// <summary>First name, for greetings.</summary>
        public string FirstName { get; init; } = string.Empty;

        /// <summary>First and last name.</summary>
        public string FullName { get; init; } = string.Empty;

        /// <summary>Email address (also the user name).</summary>
        public string Email { get; init; } = string.Empty;

        /// <summary>Phone number, if given.</summary>
        public string? PhoneNumber { get; init; }

        /// <summary>Date of birth.</summary>
        public DateTime DateOfBirth { get; init; }

        /// <summary>Age in years.</summary>
        public int Age { get; init; }

        /// <summary>Gender.</summary>
        public Gender Gender { get; init; }

        /// <summary>The user's role: Admin, Trainer or Client.</summary>
        public string Role { get; init; } = string.Empty;

        /// <summary>Whether the account may sign in.</summary>
        public AccountStatus AccountStatus { get; init; }

        /// <summary>Registration time.</summary>
        public DateTime CreatedAt { get; init; }

        /// <summary>Time of the last successful sign-in.</summary>
        public DateTime? LastLoginAt { get; init; }
    }
}
