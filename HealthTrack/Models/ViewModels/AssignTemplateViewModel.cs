using System.ComponentModel.DataAnnotations;
using HealthTrack.Models.Enums;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// The form for copying a template to one of the trainer's clients (UC20, BR-12).
    /// </summary>
    public class AssignTemplateViewModel
    {
        /// <summary>The template (from the route).</summary>
        [BindNever]
        public int TemplateId { get; set; }

        /// <summary>The client.</summary>
        [Required(ErrorMessage = "Choose a client.")]
        [Display(Name = "Client")]
        public int? ClientId { get; set; }

        /// <summary>The first day of the client's plan.</summary>
        [Required(ErrorMessage = "Choose a start date.")]
        [DataType(DataType.Date)]
        [Display(Name = "Start date")]
        public DateTime? StartDate { get; set; }

        /// <summary>Activate the copy straight away, completing the client's current plan (BR-09).</summary>
        [Display(Name = "Activate it now")]
        public bool ActivateNow { get; set; } = true;

        /// <summary>The template's title; shown only.</summary>
        [BindNever]
        [ValidateNever]
        public string TemplateTitle { get; set; } = string.Empty;

        /// <summary>The template's difficulty; shown only.</summary>
        [BindNever]
        public DifficultyLevel Difficulty { get; set; }

        /// <summary>The template's author; shown only.</summary>
        [BindNever]
        [ValidateNever]
        public string AuthorName { get; set; } = string.Empty;

        /// <summary>The template's training days; shown only.</summary>
        [BindNever]
        [ValidateNever]
        public IReadOnlyList<PlanDayViewModel> Days { get; set; } = [];

        /// <summary>The trainer's assigned clients; filled by the service, never posted.</summary>
        [BindNever]
        [ValidateNever]
        public IReadOnlyList<SelectOptionViewModel> ClientOptions { get; set; } = [];
    }
}
