namespace HealthTrack.Tests;

/// <summary>
/// Phase 0 placeholder so <c>dotnet test</c> has a test to run; remove it once real tests exist.
/// </summary>
[TestClass]
public sealed class PlaceholderTests
{
    /// <summary>
    /// Confirms the project reference to the web project resolves and its assembly loads.
    /// </summary>
    [TestMethod]
    public void WebProject_IsReferenced_AssemblyLoads()
    {
        var assembly = typeof(HealthTrack.Controllers.HomeController).Assembly;

        Assert.AreEqual("HealthTrack", assembly.GetName().Name);
    }
}
