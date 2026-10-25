using HealthTrack.Models.Entities;
using HealthTrack.Models.Enums;

namespace HealthTrack.Tests.Entities;

/// <summary>
/// Account details and the sign-in status check (BR-03, BR-05).
/// </summary>
[TestClass]
public sealed class ApplicationUserTests
{
    /// <summary>Only Active accounts may sign in.</summary>
    [TestMethod]
    [DataRow(AccountStatus.Active, true)]
    [DataRow(AccountStatus.PendingApproval, false)]
    [DataRow(AccountStatus.Suspended, false)]
    [DataRow(AccountStatus.Deactivated, false)]
    public void CanSignIn_EachStatus_TrueOnlyWhenActive(AccountStatus status, bool expected)
    {
        Assert.AreEqual(expected, new ApplicationUser { AccountStatus = status }.CanSignIn());
    }

    /// <summary>FullName is the first and last name.</summary>
    [TestMethod]
    public void FullName_FirstAndLast_JoinedWithSpace()
    {
        Assert.AreEqual("Amara Perera", new ApplicationUser { FirstName = "Amara", LastName = "Perera" }.FullName);
    }
}
