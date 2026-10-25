using System.ComponentModel.DataAnnotations;
using HealthTrack.Security;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// The form for assigning a trainer to a client (UC31). Only approved, active trainers with room appear (BR-07).
    /// </summary>
    public class AssignTrainerViewModel
    {
        /// <summary>The client.</summary>
        [Required(ErrorMessage = "Choose a client.")]
        [Display(Name = "Client")]
        public int? ClientId { get; set; }

        /// <summary>The trainer.</summary>
        [Required(ErrorMessage = "Choose a trainer.")]
        [Display(Name = "Trainer")]
        public int? TrainerId { get; set; }

        /// <summary>The first day of the assignment; the client's current assignment ends that day (BR-06).</summary>
        [Required(ErrorMessage = "Choose a start date.")]
        [NotInFuture]
        [DataType(DataType.Date)]
        [Display(Name = "Start date")]
        public DateTime? StartDate { get; set; }

        /// <summary>A note on the match.</summary>
        [StringLength(500)]
        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        /// <summary>The clients to choose from; filled by the service, never posted.</summary>
        [BindNever]
        [ValidateNever]
        public IReadOnlyList<SelectOptionViewModel> ClientOptions { get; set; } = [];

        /// <summary>The trainers with room for another client; filled by the service, never posted.</summary>
        [BindNever]
        [ValidateNever]
        public IReadOnlyList<SelectOptionViewModel> TrainerOptions { get; set; } = [];
    }
}
