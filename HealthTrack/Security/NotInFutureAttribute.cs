using System.ComponentModel.DataAnnotations;
using HealthTrack.Helpers;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace HealthTrack.Security
{
    /// <summary>
    /// Validates that a date or date and time is not later than now (BR-13). A date without a time counts as midnight,
    /// so today is always allowed. Checked on the server with <see cref="IClock"/> and in the browser by wwwroot/js/validators.js.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
    public sealed class NotInFutureAttribute : ValidationAttribute, IClientModelValidator
    {
        /// <summary>
        /// Creates the attribute with the default message.
        /// </summary>
        public NotInFutureAttribute()
            : base("{0} can't be in the future.")
        {
        }

        /// <inheritdoc />
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            // An empty value is [Required]'s job.
            if (value is not DateTime dateTime)
            {
                return ValidationResult.Success;
            }

            var now = (validationContext.GetService(typeof(IClock)) as IClock ?? new SystemClock()).Now;
            if (dateTime <= now)
            {
                return ValidationResult.Success;
            }

            return new ValidationResult(FormatErrorMessage(validationContext.DisplayName),
                validationContext.MemberName is { } member ? [member] : null);
        }

        /// <summary>
        /// Adds the data-val attributes for the browser rule, which compares with the browser's current time.
        /// </summary>
        /// <param name="context">The client validation context.</param>
        public void AddValidation(ClientModelValidationContext context)
        {
            context.Attributes.TryAdd("data-val", "true");
            context.Attributes.TryAdd("data-val-notinfuture", FormatErrorMessage(context.ModelMetadata.GetDisplayName()));
        }
    }
}
