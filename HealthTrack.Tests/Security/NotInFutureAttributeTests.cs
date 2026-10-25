using System.ComponentModel.DataAnnotations;
using HealthTrack.Helpers;
using HealthTrack.Security;
using HealthTrack.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace HealthTrack.Tests.Security;

/// <summary>
/// BR-13: logs and measurements can't be future-dated, checked against a fixed clock (now is 25 October 2026, 09:00).
/// </summary>
[TestClass]
public sealed class NotInFutureAttributeTests
{
    private static readonly IServiceProvider Services =
        new ServiceCollection().AddSingleton<IClock>(new FixedClock(TestHost.DefaultNow)).BuildServiceProvider();

    private static bool IsValid(DateTime? value) =>
        new NotInFutureAttribute().GetValidationResult(value, new ValidationContext(new object(), Services, null)
        {
            MemberName = "WorkoutDate",
            DisplayName = "Date"
        }) == ValidationResult.Success;

    /// <summary>Now itself is allowed; one minute later is not.</summary>
    [TestMethod]
    public void IsValid_AroundNow_AllowsNowButNotAMinuteLater()
    {
        Assert.IsTrue(IsValid(TestHost.DefaultNow));
        Assert.IsFalse(IsValid(TestHost.DefaultNow.AddMinutes(1)));
    }

    /// <summary>A date without a time counts as midnight, so today is allowed and tomorrow is not.</summary>
    [TestMethod]
    [DataRow(-30, true)]
    [DataRow(-1, true)]
    [DataRow(0, true)]
    [DataRow(1, false)]
    public void IsValid_DateOnly_TodayAndEarlierAllowed(int daysFromToday, bool expected)
    {
        Assert.AreEqual(expected, IsValid(TestHost.DefaultNow.Date.AddDays(daysFromToday)));
    }

    /// <summary>An empty value is left to [Required].</summary>
    [TestMethod]
    public void IsValid_Null_IsValid()
    {
        Assert.IsTrue(IsValid(null));
    }

    /// <summary>The message names the field.</summary>
    [TestMethod]
    public void IsValid_Future_MessageNamesTheField()
    {
        var result = new NotInFutureAttribute().GetValidationResult(TestHost.DefaultNow.AddDays(2),
            new ValidationContext(new object(), Services, null) { MemberName = "WorkoutDate", DisplayName = "Date" });

        Assert.AreEqual("Date can't be in the future.", result?.ErrorMessage);
    }
}
