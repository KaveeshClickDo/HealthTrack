using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models.Enums
{
    /// <summary>
    /// A user's gender, stored on AspNetUsers. It also picks the Mifflin–St Jeor constant used for BMR (spec 1.10).
    /// </summary>
    public enum Gender
    {
        /// <summary>Male (BMR constant +5).</summary>
        [Display(Name = "Male")]
        Male = 1,

        /// <summary>Female (BMR constant −161).</summary>
        [Display(Name = "Female")]
        Female = 2,

        /// <summary>Other (BMR constant −78).</summary>
        [Display(Name = "Other")]
        Other = 3,

        /// <summary>Prefer not to say (BMR constant −78).</summary>
        [Display(Name = "Prefer not to say")]
        PreferNotToSay = 4
    }
}
