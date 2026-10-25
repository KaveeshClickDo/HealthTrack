namespace HealthTrack.Services
{
    /// <summary>
    /// The outcome of a sign-in attempt; each failure has its own message on the login page.
    /// </summary>
    public enum LoginStatus
    {
        /// <summary>Signed in.</summary>
        Succeeded = 1,

        /// <summary>Unknown email or wrong password; both get the same message.</summary>
        InvalidCredentials = 2,

        /// <summary>Locked for 15 minutes after 5 failed attempts.</summary>
        LockedOut = 3,

        /// <summary>The password is right but the trainer account isn't approved yet (BR-03).</summary>
        PendingApproval = 4,

        /// <summary>The password is right but the account is suspended or deactivated.</summary>
        Inactive = 5
    }
}
