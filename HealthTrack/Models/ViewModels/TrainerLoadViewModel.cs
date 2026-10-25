using HealthTrack.Models.Enums;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// One approved trainer's active clients against their capacity (BR-07).
    /// </summary>
    public class TrainerLoadViewModel
    {
        /// <summary>The trainer profile's key.</summary>
        public int TrainerId { get; init; }

        /// <summary>The trainer's name.</summary>
        public string FullName { get; init; } = string.Empty;

        /// <summary>The trainer's profession.</summary>
        public ProfessionalType ProfessionalType { get; init; }

        /// <summary>Whether the trainer's account may sign in.</summary>
        public AccountStatus AccountStatus { get; init; }

        /// <summary>Clients with an active assignment.</summary>
        public int ActiveClients { get; init; }

        /// <summary>The trainer's capacity.</summary>
        public int MaxClients { get; init; }

        /// <summary>Whether the trainer can take another client.</summary>
        public bool HasCapacity => ActiveClients < MaxClients;

        /// <summary>Active clients as a percentage of capacity, 0–100.</summary>
        public int LoadPercent => MaxClients <= 0 ? 100 : Math.Min(100, ActiveClients * 100 / MaxClients);
    }
}
