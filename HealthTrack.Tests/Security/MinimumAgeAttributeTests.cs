using System.ComponentModel.DataAnnotations;
using System.Globalization;
using HealthTrack.Helpers;
using HealthTrack.Models.ViewModels;
using HealthTrack.Security;
using HealthTrack.Tests.TestSupport;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace HealthTrack.Tests.Security;

/// <summary>
/// BR-02: users must be 16 or older, checked against a fixed clock (today is 25 October 2026).
/// </summary>
[TestClass]
public sealed class MinimumAgeAttributeTests
{
    private static readonly IServiceProvider Services =
        new ServiceCollection().AddSingleton<IClock>(new FixedClock(TestHost.DefaultNow)).BuildServiceProvider();

    private static ValidationResult? Validate(object? value) =>
        new MinimumAgeAttribute(16).GetValidationResult(value, new ValidationContext(new object(), Services, null)
        {
            MemberName = "DateOfBirth",
            DisplayName = "Date of birth"
        });

    /// <summary>The 16th birthday is the first allowed day; the day before is refused, as is a future date.</summary>
    [TestMethod]
    [DataRow("2010-10-25", true)]
    [DataRow("2010-10-26", false)]
    [DataRow("1950-06-01", true)]
    [DataRow("2026-10-25", false)]
    [DataRow("2030-01-01", false)]
    public void IsValid_AroundSixteenthBirthday_AllowsSixteenAndOver(string dateOfBirth, bool expected)
    {
        var result = Validate(DateTime.Parse(dateOfBirth, CultureInfo.InvariantCulture));

        Assert.AreEqual(expected, result == ValidationResult.Success);
    }

    /// <summary>An empty value is left to [Required].</summary>
    [TestMethod]
    public void IsValid_Null_IsValid()
    {
        Assert.AreEqual(ValidationResult.Success, Validate(null));
    }

    /// <summary>The message names the minimum age and the field.</summary>
    [TestMethod]
    public void IsValid_TooYoung_MessageNamesTheAgeAndField()
    {
        var result = Validate(new DateTime(2015, 1, 1));

        Assert.IsNotNull(result);
        Assert.AreEqual("You must be at least 16 years old.", result.ErrorMessage);
        CollectionAssert.AreEqual(new[] { "DateOfBirth" }, result.MemberNames.ToArray());
    }

    /// <summary>The browser rule gets the latest allowed date of birth from the server's clock.</summary>
    [TestMethod]
    public void AddValidation_FixedClock_EmitsLatestAllowedDate()
    {
        var actionContext = new ActionContext(new DefaultHttpContext { RequestServices = Services }, new RouteData(), new ActionDescriptor());
        var metadataProvider = new EmptyModelMetadataProvider();
        var metadata = metadataProvider.GetMetadataForProperty(typeof(RegisterClientViewModel), nameof(RegisterClientViewModel.DateOfBirth));
        var attributes = new Dictionary<string, string>();

        new MinimumAgeAttribute(16).AddValidation(new ClientModelValidationContext(actionContext, metadata, metadataProvider, attributes));

        Assert.AreEqual("true", attributes["data-val"]);
        Assert.AreEqual("2010-10-25", attributes["data-val-minimumage-max"]);
        Assert.AreEqual("You must be at least 16 years old.", attributes["data-val-minimumage"]);
    }
}
