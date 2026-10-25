using HealthTrack.Helpers;

namespace HealthTrack.Tests.TestSupport;

/// <summary>
/// Assertions for business rule failures.
/// </summary>
public static class RuleAssert
{
    /// <summary>
    /// Asserts that <paramref name="action"/> throws a <see cref="BusinessRuleException"/> for the given rule.
    /// </summary>
    /// <param name="ruleId">The expected rule ID, for example "BR-16".</param>
    /// <param name="action">The code that should break the rule.</param>
    /// <returns>The exception, for further checks on its message.</returns>
    public static BusinessRuleException Broken(string ruleId, Action action)
    {
        var exception = Assert.ThrowsExactly<BusinessRuleException>(action);
        Assert.AreEqual(ruleId, exception.RuleId);
        return exception;
    }
}
