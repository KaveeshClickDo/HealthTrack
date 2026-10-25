using System.ComponentModel.DataAnnotations;
using System.Globalization;
using HealthTrack.Helpers;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace HealthTrack.Security
{
    /// <summary>
    /// Validates that a date of birth makes the person at least <see cref="Years"/> old today (BR-02).
    /// Checked on the server with <see cref="IClock"/> and in the browser by wwwroot/js/validators.js.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
    public sealed class MinimumAgeAttribute : ValidationAttribute, IClientModelValidator
    {
        /// <summary>
        /// Creates the attribute.
        /// </summary>
        /// <param name="years">The minimum age in whole years.</param>
        public MinimumAgeAttribute(int years)
            : base("You must be at least {1} years old.")
        {
            Years = years;
        }

        /// <summary>The minimum age in whole years.</summary>
        public int Years { get; }

        /// <inheritdoc />
        public override string FormatErrorMessage(string name) =>
            string.Format(CultureInfo.CurrentCulture, ErrorMessageString, name, Years);

        /// <inheritdoc />
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            // An empty value is [Required]'s job.
            if (value is not DateTime dateOfBirth)
            {
                return ValidationResult.Success;
            }

            var today = ResolveClock(validationContext).Today;
            if (HealthCalculator.CalculateAge(dateOfBirth, today) >= Years)
            {
                return ValidationResult.Success;
            }

            return new ValidationResult(FormatErrorMessage(validationContext.DisplayName),
                validationContext.MemberName is { } member ? [member] : null);
        }

        /// <summary>
        /// Adds the data-val attributes for the browser rule: the latest allowed date of birth, from the server's clock.
        /// </summary>
        /// <param name="context">The client validation context.</param>
        public void AddValidation(ClientModelValidationContext context)
        {
            var clock = context.ActionContext.HttpContext.RequestServices.GetService<IClock>() ?? new SystemClock();
            var latestDateOfBirth = clock.Today.AddYears(-Years);

            context.Attributes.TryAdd("data-val", "true");
            context.Attributes.TryAdd("data-val-minimumage", FormatErrorMessage(context.ModelMetadata.GetDisplayName()));
            context.Attributes.TryAdd("data-val-minimumage-max", latestDateOfBirth.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        private static IClock ResolveClock(ValidationContext validationContext) =>
            validationContext.GetService(typeof(IClock)) as IClock ?? new SystemClock();
    }
}
