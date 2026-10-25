using HealthTrack.Security;

namespace HealthTrack.Tests.Security;

/// <summary>
/// BR-02: the privacy-notice checkbox must be ticked.
/// </summary>
[TestClass]
public sealed class MustBeTrueAttributeTests
{
    /// <summary>Only a ticked box (true) passes.</summary>
    [TestMethod]
    [DataRow(true, true)]
    [DataRow(false, false)]
    [DataRow(null, false)]
    public void IsValid_CheckboxValue_TrueOnlyWhenTicked(bool? value, bool expected)
    {
        Assert.AreEqual(expected, new MustBeTrueAttribute().IsValid(value));
    }
}
