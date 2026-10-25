using System.ComponentModel.DataAnnotations;
using HealthTrack.Helpers;
using HealthTrack.Models.Enums;
using HealthTrack.Models.ViewModels;
using HealthTrack.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace HealthTrack.Tests.ViewModels;

/// <summary>
/// The registration forms' validation (spec 1.3, BR-01 to BR-03), run the way MVC runs it on the server.
/// </summary>
[TestClass]
public sealed class RegisterViewModelTests
{
    private static readonly IServiceProvider Services =
        new ServiceCollection().AddSingleton<IClock>(new FixedClock(TestHost.DefaultNow)).BuildServiceProvider();

    private static RegisterTrainerViewModel ValidTrainer(ProfessionalType type, string? licence) => new()
    {
        FirstName = "Shanika",
        LastName = "De Silva",
        Email = "shanika@example.test",
        Password = "Trainer@123",
        ConfirmPassword = "Trainer@123",
        DateOfBirth = new DateTime(1990, 11, 3),
        Gender = Gender.Female,
        AcceptPrivacy = true,
        ProfessionalType = type,
        Specialization = "Sports nutrition",
        Qualifications = "BSc Nutrition",
        LicenseNumber = licence,
        YearsOfExperience = 6
    };

    private static List<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model, Services, null), results, validateAllProperties: true);
        return results;
    }

    /// <summary>BR-03: a fitness trainer may leave the licence number empty.</summary>
    [TestMethod]
    public void Validate_FitnessTrainerWithoutLicence_IsValid()
    {
        Assert.IsEmpty(Validate(ValidTrainer(ProfessionalType.FitnessTrainer, null)));
    }

    /// <summary>BR-03: every other profession needs a licence number.</summary>
    [TestMethod]
    [DataRow(ProfessionalType.Nutritionist)]
    [DataRow(ProfessionalType.Physiotherapist)]
    [DataRow(ProfessionalType.MedicalDoctor)]
    public void Validate_OtherProfessionWithoutLicence_ErrorOnLicenseNumber(ProfessionalType type)
    {
        var error = Validate(ValidTrainer(type, "  ")).Single();

        Assert.AreEqual(RegisterTrainerViewModel.LicenseRequiredMessage, error.ErrorMessage);
        CollectionAssert.AreEqual(new[] { nameof(RegisterTrainerViewModel.LicenseNumber) }, error.MemberNames.ToArray());
    }

    /// <summary>With a licence number, any profession is valid.</summary>
    [TestMethod]
    public void Validate_NutritionistWithLicence_IsValid()
    {
        Assert.IsEmpty(Validate(ValidTrainer(ProfessionalType.Nutritionist, "NUT-20417")));
    }

    /// <summary>BR-02: registration needs the privacy notice accepted.</summary>
    [TestMethod]
    public void Validate_PrivacyNotAccepted_ErrorOnAcceptPrivacy()
    {
        var model = ValidTrainer(ProfessionalType.FitnessTrainer, null);
        model.AcceptPrivacy = false;

        var error = Validate(model).Single();

        Assert.AreEqual(RegisterViewModel.ConsentMessage, error.ErrorMessage);
    }

    /// <summary>BR-01: weak passwords are refused before Identity sees them.</summary>
    [TestMethod]
    [DataRow("short1!")]
    [DataRow("alllowercase1!")]
    [DataRow("ALLUPPERCASE1!")]
    [DataRow("NoDigitsHere!")]
    [DataRow("NoSymbols123")]
    public void Validate_WeakPassword_ErrorOnPassword(string password)
    {
        var model = ValidTrainer(ProfessionalType.FitnessTrainer, null);
        model.Password = password;
        model.ConfirmPassword = password;

        Assert.IsTrue(Validate(model).Any(e => e.MemberNames.Contains(nameof(RegisterViewModel.Password))));
    }

    /// <summary>Names allow letters (including accented ones), spaces and hyphens only.</summary>
    [TestMethod]
    [DataRow("Anne-Marie", true)]
    [DataRow("De Silva", true)]
    [DataRow("José", true)]
    [DataRow("Bob1", false)]
    [DataRow("-Bob", false)]
    [DataRow("Bob  Smith", false)]
    [DataRow("<script>", false)]
    public void Validate_FirstName_FollowsNamePattern(string firstName, bool expected)
    {
        var model = ValidTrainer(ProfessionalType.FitnessTrainer, null);
        model.FirstName = firstName;

        Assert.AreEqual(expected, !Validate(model).Any(e => e.MemberNames.Contains(nameof(RegisterViewModel.FirstName))));
    }
}
