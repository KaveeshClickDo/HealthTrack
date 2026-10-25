using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models.Enums
{
    /// <summary>
    /// The kind of event an AuditLog row records.
    /// </summary>
    public enum AuditAction
    {
        /// <summary>A successful sign-in.</summary>
        [Display(Name = "Login")]
        Login = 1,

        /// <summary>A failed sign-in (unknown email or wrong password).</summary>
        [Display(Name = "Login failed")]
        LoginFailed = 2,

        /// <summary>A sign-out.</summary>
        [Display(Name = "Logout")]
        Logout = 3,

        /// <summary>A new client or trainer registration.</summary>
        [Display(Name = "Register")]
        Register = 4,

        /// <summary>A record was created.</summary>
        [Display(Name = "Create")]
        Create = 5,

        /// <summary>A record was changed.</summary>
        [Display(Name = "Update")]
        Update = 6,

        /// <summary>A record or account was deleted.</summary>
        [Display(Name = "Delete")]
        Delete = 7,

        /// <summary>Something was approved, for example a trainer account.</summary>
        [Display(Name = "Approve")]
        Approve = 8,

        /// <summary>Something was rejected, for example a trainer account.</summary>
        [Display(Name = "Reject")]
        Reject = 9,

        /// <summary>Something was cancelled, for example an appointment.</summary>
        [Display(Name = "Cancel")]
        Cancel = 10,

        /// <summary>A trainer was assigned to a client.</summary>
        [Display(Name = "Assign")]
        Assign = 11,

        /// <summary>Data was exported, for example a report CSV or "Download my data".</summary>
        [Display(Name = "Export")]
        Export = 12,

        /// <summary>An account was locked after too many failed sign-ins.</summary>
        [Display(Name = "Account locked")]
        AccountLocked = 13
    }
}
