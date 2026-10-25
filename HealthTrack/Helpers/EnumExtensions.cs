using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace HealthTrack.Helpers
{
    /// <summary>
    /// Helpers for the enums' [Display] names.
    /// </summary>
    public static class EnumExtensions
    {
        /// <summary>
        /// The member's [Display(Name)] label, for example "Cancelled by client"; the member name when there is none.
        /// </summary>
        /// <param name="value">An enum value.</param>
        /// <returns>The label to show to users.</returns>
        public static string GetDisplayName(this Enum value)
        {
            var member = value.GetType().GetMember(value.ToString()).FirstOrDefault();
            return member?.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? value.ToString();
        }
    }
}
