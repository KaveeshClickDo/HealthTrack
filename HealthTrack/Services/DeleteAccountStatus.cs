namespace HealthTrack.Services
{
    /// <summary>
    /// The outcome of <see cref="IAccountService.DeleteAccountAsync"/> (UC16, BR-22).
    /// </summary>
    public enum DeleteAccountStatus
    {
        /// <summary>The health records are gone and the account is anonymised; the user is signed out.</summary>
        Deleted = 1,

        /// <summary>The password was wrong; nothing changed.</summary>
        WrongPassword = 2,

        /// <summary>Too many wrong passwords: the account is locked for a while (the same lockout as sign-in); nothing changed.</summary>
        LockedOut = 3,

        /// <summary>The user doesn't exist or isn't a client.</summary>
        NotFound = 4
    }
}
