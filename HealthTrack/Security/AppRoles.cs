namespace HealthTrack.Security
{
    /// <summary>
    /// The three role names (spec 1.2). Each user has exactly one (BR-05).
    /// </summary>
    public static class AppRoles
    {
        /// <summary>Platform operators.</summary>
        public const string Admin = "Admin";

        /// <summary>Fitness trainers and health professionals.</summary>
        public const string Trainer = "Trainer";

        /// <summary>Members tracking their own health.</summary>
        public const string Client = "Client";

        /// <summary>All roles, in the order the seeder creates them.</summary>
        public static readonly IReadOnlyList<string> All = [Admin, Trainer, Client];
    }
}
