using System.Security.Claims;
using HealthTrack.Data;
using HealthTrack.Helpers;
using HealthTrack.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HealthTrack.Security
{
    /// <summary>
    /// Adds the person's name and, for professionals, their type to the sign-in cookie (AppClaims), for the sidebar's
    /// profile card. Identity rebuilds the principal when it re-validates the security stamp (every minute), so a
    /// changed name shows up without signing out.
    /// </summary>
    public class HealthTrackClaimsPrincipalFactory : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
    {
        private readonly ApplicationDbContext _db;

        /// <summary>Creates the factory with Identity's managers and the database.</summary>
        /// <param name="userManager">The user manager.</param>
        /// <param name="roleManager">The role manager.</param>
        /// <param name="options">Identity options.</param>
        /// <param name="db">The database, to read a professional's type.</param>
        public HealthTrackClaimsPrincipalFactory(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager,
            IOptions<IdentityOptions> options, ApplicationDbContext db)
            : base(userManager, roleManager, options)
        {
            _db = db;
        }

        /// <summary>The standard claims plus the full name and, for professionals, the professional type.</summary>
        /// <param name="user">The user signing in.</param>
        /// <returns>The claims identity.</returns>
        protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
        {
            var identity = await base.GenerateClaimsAsync(user);
            identity.AddClaim(new Claim(AppClaims.FullName, user.FullName));

            var professionalType = await _db.TrainerProfiles.AsNoTracking()
                .Where(t => t.UserId == user.Id)
                .Select(t => (Models.Enums.ProfessionalType?)t.ProfessionalType)
                .FirstOrDefaultAsync();
            if (professionalType is not null)
            {
                identity.AddClaim(new Claim(AppClaims.Profession, professionalType.Value.GetDisplayName()));
            }

            return identity;
        }
    }
}
