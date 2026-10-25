using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models.Enums
{
    /// <summary>
    /// What an appointment is for (spec 1.6).
    /// </summary>
    public enum AppointmentType
    {
        /// <summary>A personal training session.</summary>
        [Display(Name = "Personal training")]
        PersonalTraining = 1,

        /// <summary>A fitness assessment.</summary>
        [Display(Name = "Fitness assessment")]
        FitnessAssessment = 2,

        /// <summary>A nutrition consultation.</summary>
        [Display(Name = "Nutrition consultation")]
        NutritionConsultation = 3,

        /// <summary>A physiotherapy session.</summary>
        [Display(Name = "Physiotherapy")]
        Physiotherapy = 4,

        /// <summary>A medical consultation.</summary>
        [Display(Name = "Medical consultation")]
        MedicalConsultation = 5,

        /// <summary>A follow-up on an earlier appointment.</summary>
        [Display(Name = "Follow-up")]
        FollowUp = 6
    }
}
