using HealthTrack.Models.Enums;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// A trainer registration waiting for approval (UC30), with everything the administrator checks.
    /// </summary>
    public class PendingTrainerViewModel
    {
        /// <summary>The trainer profile's key.</summary>
        public int TrainerId { get; init; }

        /// <summary>First and last name.</summary>
        public string FullName { get; init; } = string.Empty;

        /// <summary>Email address.</summary>
        public string Email { get; init; } = string.Empty;

        /// <summary>Phone number, if given.</summary>
        public string? PhoneNumber { get; init; }

        /// <summary>The kind of professional.</summary>
        public ProfessionalType ProfessionalType { get; init; }

        /// <summary>Area of expertise.</summary>
        public string Specialization { get; init; } = string.Empty;

        /// <summary>Certificates and degrees.</summary>
        public string Qualifications { get; init; } = string.Empty;

        /// <summary>Licence number, if any.</summary>
        public string? LicenseNumber { get; init; }

        /// <summary>Years of experience.</summary>
        public int YearsOfExperience { get; init; }

        /// <summary>Short biography.</summary>
        public string? Bio { get; init; }

        /// <summary>When the trainer registered.</summary>
        public DateTime RegisteredAt { get; init; }
    }
}
