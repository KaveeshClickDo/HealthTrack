using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// Filters for the assignment list.
    /// </summary>
    public class AssignmentFilterViewModel
    {
        /// <summary>Part of the client's or trainer's name, or the client's email.</summary>
        [StringLength(256)]
        [Display(Name = "Client or trainer")]
        public string? Search { get; set; }

        /// <summary>Hide ended assignments.</summary>
        [Display(Name = "Active only")]
        public bool ActiveOnly { get; set; }
    }
}
