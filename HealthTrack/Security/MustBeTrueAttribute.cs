using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Security
{
    /// <summary>
    /// Validates that a checkbox is ticked, for example the privacy-notice consent (BR-02). Server side only: in the browser,
    /// the implicit required rule MVC adds to a bool already demands a ticked checkbox. ([Range(true, true)] can't be used
    /// instead, because jQuery Validation compares "true" with "True" as strings and always fails.)
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
    public sealed class MustBeTrueAttribute : ValidationAttribute
    {
        /// <summary>
        /// Creates the attribute with the default message.
        /// </summary>
        public MustBeTrueAttribute()
            : base("{0} must be ticked.")
        {
        }

        /// <inheritdoc />
        public override bool IsValid(object? value) => value is true;
    }
}
