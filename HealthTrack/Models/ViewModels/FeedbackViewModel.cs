using System.ComponentModel.DataAnnotations;
using HealthTrack.Models.Enums;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// The form a trainer uses to give an assigned client feedback, optionally about one session (UC22).
    /// </summary>
    public class FeedbackViewModel
    {
        /// <summary>The client; the service checks they are assigned to the trainer (BR-08).</summary>
        [Required(ErrorMessage = "Choose a client.")]
        [Display(Name = "Client")]
        public int? ClientId { get; set; }

        /// <summary>The session the feedback is about, if any; it must be the client's own.</summary>
        [Display(Name = "About a session")]
        public int? WorkoutLogId { get; set; }

        /// <summary>What the feedback is about.</summary>
        [Required(ErrorMessage = "Choose a category.")]
        [Display(Name = "Category")]
        public FeedbackCategory? Category { get; set; }

        /// <summary>The comment.</summary>
        [Required(ErrorMessage = "Write your feedback.")]
        [StringLength(1000)]
        [Display(Name = "Feedback")]
        public string Message { get; set; } = string.Empty;

        /// <summary>Progress rating, 1–5; optional.</summary>
        [Range(1, 5, ErrorMessage = "The rating must be between 1 and 5.")]
        [Display(Name = "Progress rating")]
        public int? Rating { get; set; }

        /// <summary>The client's name once chosen; shown only.</summary>
        [BindNever]
        [ValidateNever]
        public string? ClientName { get; set; }

        /// <summary>The trainer's assigned clients; filled by the service, never posted.</summary>
        [BindNever]
        [ValidateNever]
        public IReadOnlyList<SelectOptionViewModel> ClientOptions { get; set; } = [];

        /// <summary>The chosen client's recent sessions; filled by the service, never posted.</summary>
        [BindNever]
        [ValidateNever]
        public IReadOnlyList<SelectOptionViewModel> SessionOptions { get; set; } = [];
    }
}
