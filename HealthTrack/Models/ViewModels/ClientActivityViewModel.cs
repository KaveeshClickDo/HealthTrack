using HealthTrack.Models.Enums;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// A client's activity totals for the admin's user details page. Counts only: never clinical notes or measurement values
    /// (spec 1.2: admins see totals).
    /// </summary>
    public class ClientActivityViewModel
    {
        /// <summary>The client profile's key.</summary>
        public int ClientId { get; init; }

        /// <summary>The client's goal.</summary>
        public FitnessGoal FitnessGoal { get; init; }

        /// <summary>The client's activity level.</summary>
        public ActivityLevel ActivityLevel { get; init; }

        /// <summary>The current trainer's name, or null when unassigned.</summary>
        public string? TrainerName { get; init; }

        /// <summary>Workout sessions logged.</summary>
        public int WorkoutsLogged { get; init; }

        /// <summary>Meals logged.</summary>
        public int MealsLogged { get; init; }

        /// <summary>Measurements recorded (the number, not the values).</summary>
        public int MeasurementsRecorded { get; init; }

        /// <summary>Appointments booked.</summary>
        public int Appointments { get; init; }

        /// <summary>The most recent workout date.</summary>
        public DateTime? LastWorkoutDate { get; init; }

        /// <summary>The client's workout and diet plans, for the plan correction (EXT-02).</summary>
        public IReadOnlyList<ClientPlanViewModel> Plans { get; set; } = [];
    }
}
