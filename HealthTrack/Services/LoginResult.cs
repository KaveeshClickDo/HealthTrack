namespace HealthTrack.Services
{
    /// <summary>
    /// The outcome of <see cref="IAccountService.LoginAsync"/>.
    /// </summary>
    public sealed class LoginResult
    {
        private LoginResult(LoginStatus status, string? role)
        {
            Status = status;
            Role = role;
        }

        /// <summary>What happened.</summary>
        public LoginStatus Status { get; }

        /// <summary>The signed-in user's role, for the dashboard redirect; null unless signed in.</summary>
        public string? Role { get; }

        /// <summary>
        /// A successful sign-in.
        /// </summary>
        /// <param name="role">The user's role.</param>
        /// <returns>The result.</returns>
        public static LoginResult Success(string? role) => new(LoginStatus.Succeeded, role);

        /// <summary>
        /// A refused sign-in.
        /// </summary>
        /// <param name="status">Why it was refused.</param>
        /// <returns>The result.</returns>
        public static LoginResult Failed(LoginStatus status) => new(status, null);
    }
}
