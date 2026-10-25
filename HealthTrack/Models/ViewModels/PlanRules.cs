using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// Validation the plan forms share, so the create and edit forms apply the same rules.
    /// </summary>
    public static class PlanRules
    {
        /// <summary>
        /// A client plan needs a start date (and, when creating, a client); the end date can't be before the start (BR-10).
        /// Templates have no client and no dates.
        /// </summary>
        /// <param name="isTemplate">Whether the plan is a template.</param>
        /// <param name="clientId">The chosen client.</param>
        /// <param name="startDate">The start date.</param>
        /// <param name="endDate">The end date.</param>
        /// <param name="requireClient">Whether the form chooses the client (only the create form does).</param>
        /// <returns>The errors, if any.</returns>
        public static IEnumerable<ValidationResult> ValidateDates(bool isTemplate, int? clientId, DateTime? startDate,
            DateTime? endDate, bool requireClient)
        {
            if (isTemplate)
            {
                yield break;
            }

            if (requireClient && clientId is null)
            {
                yield return new ValidationResult("Choose a client.", ["ClientId"]);
            }

            if (startDate is null)
            {
                yield return new ValidationResult("Choose a start date.", ["StartDate"]);
            }
            else if (endDate is { } end && end.Date < startDate.Value.Date)
            {
                yield return new ValidationResult("The end date can't be before the start date.", ["EndDate"]);
            }
        }
    }
}
