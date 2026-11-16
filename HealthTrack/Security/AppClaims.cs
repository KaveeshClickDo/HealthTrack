namespace HealthTrack.Security
{
    /// <summary>
    /// Extra claims stored in the sign-in cookie, so the layout can greet people by name without a database query.
    /// </summary>
    public static class AppClaims
    {
        /// <summary>First and last name.</summary>
        public const string FullName = "ht:full_name";

        /// <summary>A professional's type as shown to people ("Fitness trainer"); only on trainer accounts.</summary>
        public const string Profession = "ht:profession";
    }
}
