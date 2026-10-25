using HealthTrack.Models.Enums;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// The admin's view of one account (UC29): account details, activity totals and recent audit events.
    /// </summary>
    public class UserDetailsViewModel
    {
        /// <summary>The user's ID.</summary>
        public string UserId { get; init; } = string.Empty;

        /// <summary>First and last name.</summary>
        public string FullName { get; init; } = string.Empty;

        /// <summary>Email address.</summary>
        public string Email { get; init; } = string.Empty;

        /// <summary>Phone number, if given.</summary>
        public string? PhoneNumber { get; init; }

        /// <summary>Date of birth.</summary>
        public DateTime DateOfBirth { get; init; }

        /// <summary>Age in years.</summary>
        public int Age { get; init; }

        /// <summary>Gender.</summary>
        public Gender Gender { get; init; }

        /// <summary>The user's role.</summary>
        public string Role { get; init; } = string.Empty;

        /// <summary>Whether the account may sign in.</summary>
        public AccountStatus AccountStatus { get; init; }

        /// <summary>Registration time.</summary>
        public DateTime CreatedAt { get; init; }

        /// <summary>Last successful sign-in.</summary>
        public DateTime? LastLoginAt { get; init; }

        /// <summary>When a lockout ends, if the account is locked out now.</summary>
        public DateTimeOffset? LockoutEnd { get; init; }

        /// <summary>Whether this is the signed-in administrator's own account (BR-04: they can't suspend it).</summary>
        public bool IsCurrentUser { get; init; }

        /// <summary>Client totals, when the user is a client.</summary>
        public ClientActivityViewModel? Client { get; init; }

        /// <summary>Trainer details and totals, when the user is a trainer.</summary>
        public TrainerActivityViewModel? Trainer { get; init; }

        /// <summary>The user's 10 most recent audit events.</summary>
        public IReadOnlyList<AuditLogItemViewModel> RecentEvents { get; init; } = [];
    }
}
