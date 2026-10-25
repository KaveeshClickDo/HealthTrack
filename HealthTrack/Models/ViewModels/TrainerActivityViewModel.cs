using HealthTrack.Models.Enums;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// A trainer's professional details and activity totals for the admin's user details page.
    /// </summary>
    public class TrainerActivityViewModel
    {
        /// <summary>The trainer profile's key.</summary>
        public int TrainerId { get; init; }

        /// <summary>The kind of professional.</summary>
        public ProfessionalType ProfessionalType { get; init; }

        /// <summary>Area of expertise.</summary>
        public string Specialization { get; init; } = string.Empty;

        /// <summary>Licence number, if any.</summary>
        public string? LicenseNumber { get; init; }

        /// <summary>Years of experience.</summary>
        public int YearsOfExperience { get; init; }

        /// <summary>When the account was approved; null while pending.</summary>
        public DateTime? ApprovedAt { get; init; }

        /// <summary>Clients with an active assignment.</summary>
        public int ActiveClients { get; init; }

        /// <summary>The trainer's capacity (BR-07).</summary>
        public int MaxClients { get; init; }

        /// <summary>Workout plans and templates written.</summary>
        public int WorkoutPlans { get; init; }

        /// <summary>Diet plans written.</summary>
        public int DietPlans { get; init; }

        /// <summary>Appointments with the trainer.</summary>
        public int Appointments { get; init; }
    }
}
