namespace HealthTrack.Helpers
{
    /// <summary>
    /// Thrown when a business rule from spec 1.11 is broken. Controllers catch it and show <see cref="Exception.Message"/> to the user.
    /// </summary>
    public class BusinessRuleException : InvalidOperationException
    {
        /// <summary>
        /// Creates the exception for a broken rule.
        /// </summary>
        /// <param name="ruleId">The rule's ID from spec 1.11, for example "BR-16".</param>
        /// <param name="message">A message that can be shown to the user as it is.</param>
        public BusinessRuleException(string ruleId, string message)
            : base(message)
        {
            RuleId = ruleId;
        }

        /// <summary>The ID of the broken rule, for example "BR-16".</summary>
        public string RuleId { get; }
    }
}
