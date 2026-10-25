using HealthTrack.Models.Enums;

namespace HealthTrack.Models.ViewModels.Reports
{
    /// <summary>
    /// One approved professional in the trainer workload report (admins only).
    /// </summary>
    public class TrainerWorkloadRow
    {
        /// <summary>The trainer profile's key.</summary>
        public int TrainerId { get; init; }

        /// <summary>The professional's name.</summary>
        public string TrainerName { get; init; } = string.Empty;

        /// <summary>The kind of professional.</summary>
        public ProfessionalType ProfessionalType { get; init; }

        /// <summary>Clients assigned now.</summary>
        public int ActiveClients { get; init; }

        /// <summary>The most clients the professional takes.</summary>
        public int MaxClients { get; init; }

        /// <summary>Active clients as a percentage of capacity.</summary>
        public int LoadPercent => MaxClients <= 0 ? 100 : (int)Math.Round(ActiveClients * 100m / MaxClients, MidpointRounding.AwayFromZero);

        /// <summary>Appointments starting in the range, any status.</summary>
        public int Appointments { get; init; }

        /// <summary>Appointments in the range that were held.</summary>
        public int AppointmentsCompleted { get; init; }

        /// <summary>Workout and diet plans written for clients in the range.</summary>
        public int PlansCreated { get; init; }

        /// <summary>Feedback given in the range.</summary>
        public int FeedbackGiven { get; init; }
    }
}
