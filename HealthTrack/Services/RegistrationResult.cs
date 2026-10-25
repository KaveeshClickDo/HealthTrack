using System.Diagnostics.CodeAnalysis;
using HealthTrack.Models.Entities;
using Microsoft.AspNetCore.Identity;

namespace HealthTrack.Services
{
    /// <summary>
    /// The outcome of a registration: the new user, or Identity's errors for the form.
    /// </summary>
    public sealed class RegistrationResult
    {
        private RegistrationResult(ApplicationUser? user, IReadOnlyList<IdentityError> errors)
        {
            User = user;
            Errors = errors;
        }

        /// <summary>Whether the account was created.</summary>
        [MemberNotNullWhen(true, nameof(User))]
        public bool Succeeded => User is not null;

        /// <summary>The new user, when the registration succeeded.</summary>
        public ApplicationUser? User { get; }

        /// <summary>Why the registration failed, for example a duplicate email.</summary>
        public IReadOnlyList<IdentityError> Errors { get; }

        /// <summary>
        /// A successful registration.
        /// </summary>
        /// <param name="user">The new user.</param>
        /// <returns>The result.</returns>
        public static RegistrationResult Success(ApplicationUser user) => new(user, []);

        /// <summary>
        /// A failed registration.
        /// </summary>
        /// <param name="errors">Identity's errors.</param>
        /// <returns>The result.</returns>
        public static RegistrationResult Failed(IEnumerable<IdentityError> errors) => new(null, errors.ToList());
    }
}
