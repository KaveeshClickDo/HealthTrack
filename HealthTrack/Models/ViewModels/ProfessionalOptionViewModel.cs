using HealthTrack.Models.Enums;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// One approved, active professional a client can book, with what helps them choose.
    /// </summary>
    public class ProfessionalOptionViewModel
    {
        /// <summary>The trainer profile's key.</summary>
        public int TrainerId { get; init; }

        /// <summary>The professional's name.</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>Fitness trainer, nutritionist, physiotherapist or doctor.</summary>
        public ProfessionalType ProfessionalType { get; init; }

        /// <summary>For example, strength and conditioning.</summary>
        public string Specialization { get; init; } = string.Empty;

        /// <summary>Years in practice.</summary>
        public int YearsOfExperience { get; init; }

        /// <summary>The professional's introduction, if any.</summary>
        public string? Bio { get; init; }

        /// <summary>Whether this is the client's assigned trainer.</summary>
        public bool IsMyTrainer { get; init; }

        /// <summary>The appointment type that usually goes with this professional, to pre-select.</summary>
        public AppointmentType SuggestedType => ProfessionalType switch
        {
            ProfessionalType.Nutritionist => AppointmentType.NutritionConsultation,
            ProfessionalType.Physiotherapist => AppointmentType.Physiotherapy,
            ProfessionalType.MedicalDoctor => AppointmentType.MedicalConsultation,
            _ => AppointmentType.PersonalTraining
        };
    }
}
