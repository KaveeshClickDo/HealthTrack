using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HealthTrack.Helpers;
using HealthTrack.Models.Enums;
using Microsoft.AspNetCore.Identity;

namespace HealthTrack.Models.Entities
{
    /// <summary>
    /// A login account (AspNetUsers) with personal details and status. The email address is also the user name (BR-01).
    /// </summary>
    public class ApplicationUser : IdentityUser
    {
        /// <summary>First name; letters, spaces and hyphens.</summary>
        [MaxLength(50)]
        public string FirstName { get; set; } = string.Empty;

        /// <summary>Last name; letters, spaces and hyphens.</summary>
        [MaxLength(50)]
        public string LastName { get; set; } = string.Empty;

        /// <summary>Date of birth; the user must be 16 or older (BR-02).</summary>
        [Column(TypeName = "date")]
        public DateTime DateOfBirth { get; set; }

        /// <summary>Gender, also used for BMR.</summary>
        public Gender Gender { get; set; }

        /// <summary>Whether the account may sign in (BR-03, BR-05).</summary>
        public AccountStatus AccountStatus { get; set; }

        /// <summary>When the privacy notice was accepted (BR-02).</summary>
        public DateTime PrivacyConsentAt { get; set; }

        /// <summary>Registration time.</summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>Time of the last successful sign-in.</summary>
        public DateTime? LastLoginAt { get; set; }

        /// <summary>The client profile, when the user is a Client (R3).</summary>
        public virtual ClientProfile? ClientProfile { get; set; }

        /// <summary>The trainer profile, when the user is a Trainer (R4).</summary>
        public virtual TrainerProfile? TrainerProfile { get; set; }

        /// <summary>The user's reminder settings (R5).</summary>
        public virtual NotificationPreference? NotificationPreference { get; set; }

        /// <summary>Notifications sent to the user (R6).</summary>
        public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

        /// <summary>Audited actions the user performed (R7).</summary>
        public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

        /// <summary>First and last name together.</summary>
        [NotMapped]
        public string FullName => $"{FirstName} {LastName}";

        /// <summary>Age in years today; for display only, so it reads the system date.</summary>
        [NotMapped]
        public int Age => HealthCalculator.CalculateAge(DateOfBirth, DateTime.Today);

        /// <summary>
        /// Whether the account may sign in: only Active accounts can. Pending trainers, suspended and deactivated accounts cannot (BR-03, BR-05).
        /// </summary>
        /// <returns>True when <see cref="AccountStatus"/> is Active.</returns>
        public bool CanSignIn() => AccountStatus == AccountStatus.Active;
    }
}
