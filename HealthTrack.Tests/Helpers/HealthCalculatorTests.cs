using System.Globalization;
using HealthTrack.Helpers;
using HealthTrack.Models.Enums;

namespace HealthTrack.Tests.Helpers;

/// <summary>
/// The spec 1.10 formulas in <see cref="HealthCalculator"/>.
/// </summary>
[TestClass]
public sealed class HealthCalculatorTests
{
    /// <summary>70 kg at 175 cm is 22.857…, shown to 1 decimal place.</summary>
    [TestMethod]
    public void CalculateBmi_70KgAt175Cm_RoundsTo22Point9()
    {
        Assert.AreEqual(22.9m, HealthCalculator.CalculateBmi(70m, 175m));
    }

    /// <summary>95 kg at 180 cm is 29.32….</summary>
    [TestMethod]
    public void CalculateBmi_95KgAt180Cm_Returns29Point3()
    {
        Assert.AreEqual(29.3m, HealthCalculator.CalculateBmi(95m, 180m));
    }

    /// <summary>A zero height would divide by zero, so it is refused.</summary>
    [TestMethod]
    public void CalculateBmi_ZeroHeight_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => HealthCalculator.CalculateBmi(70m, 0m));
    }

    /// <summary>WHO boundaries: under 18.5, 18.5–24.9, 25.0–29.9, 30.0 and over.</summary>
    [TestMethod]
    [DataRow(10.0, BmiCategory.Underweight)]
    [DataRow(18.4, BmiCategory.Underweight)]
    [DataRow(18.5, BmiCategory.Normal)]
    [DataRow(24.9, BmiCategory.Normal)]
    [DataRow(25.0, BmiCategory.Overweight)]
    [DataRow(29.9, BmiCategory.Overweight)]
    [DataRow(30.0, BmiCategory.Obese)]
    [DataRow(45.0, BmiCategory.Obese)]
    public void GetBmiCategory_AtEachBoundary_ReturnsWhoCategory(double bmi, BmiCategory expected)
    {
        Assert.AreEqual(expected, HealthCalculator.GetBmiCategory((decimal)bmi));
    }

    /// <summary>The age goes up on the birthday itself, not the day before.</summary>
    [TestMethod]
    [DataRow("2000-10-25", "2026-10-25", 26)]
    [DataRow("2000-10-25", "2026-10-24", 25)]
    [DataRow("2000-10-25", "2026-12-31", 26)]
    public void CalculateAge_AroundBirthday_CountsCompletedYears(string dateOfBirth, string today, int expected)
    {
        Assert.AreEqual(expected, HealthCalculator.CalculateAge(DateTime.Parse(dateOfBirth, CultureInfo.InvariantCulture), DateTime.Parse(today, CultureInfo.InvariantCulture)));
    }

    /// <summary>Someone born on 29 February turns a year older on 1 March in a non-leap year.</summary>
    [TestMethod]
    [DataRow("2026-02-28", 17)]
    [DataRow("2026-03-01", 18)]
    public void CalculateAge_LeapDayBirthday_CountsFromFirstOfMarch(string today, int expected)
    {
        Assert.AreEqual(expected, HealthCalculator.CalculateAge(new DateTime(2008, 2, 29), DateTime.Parse(today, CultureInfo.InvariantCulture)));
    }

    /// <summary>Mifflin–St Jeor: 10W + 6.25H − 5A + s, with s = +5 male, −161 female, −78 otherwise.</summary>
    [TestMethod]
    [DataRow(80.0, 180.0, 30, Gender.Male, 1780.0)]
    [DataRow(65.0, 165.0, 28, Gender.Female, 1380.25)]
    [DataRow(70.0, 170.0, 40, Gender.Other, 1484.5)]
    [DataRow(70.0, 170.0, 40, Gender.PreferNotToSay, 1484.5)]
    public void CalculateBmr_EachGender_UsesItsConstant(double weightKg, double heightCm, int age, Gender gender, double expected)
    {
        Assert.AreEqual((decimal)expected, HealthCalculator.CalculateBmr((decimal)weightKg, (decimal)heightCm, age, gender));
    }

    /// <summary>Activity factors 1.2, 1.375, 1.55, 1.725 and 1.9.</summary>
    [TestMethod]
    [DataRow(ActivityLevel.Sedentary, 1800.0)]
    [DataRow(ActivityLevel.LightlyActive, 2062.5)]
    [DataRow(ActivityLevel.ModeratelyActive, 2325.0)]
    [DataRow(ActivityLevel.VeryActive, 2587.5)]
    [DataRow(ActivityLevel.ExtraActive, 2850.0)]
    public void CalculateTdee_EachActivityLevel_AppliesItsFactor(ActivityLevel activityLevel, double expected)
    {
        Assert.AreEqual((decimal)expected, HealthCalculator.CalculateTdee(1500m, activityLevel));
    }

    /// <summary>−500 to lose weight, +300 to gain muscle, TDEE for every other goal.</summary>
    [TestMethod]
    [DataRow(FitnessGoal.LoseWeight, 2000)]
    [DataRow(FitnessGoal.GainMuscle, 2800)]
    [DataRow(FitnessGoal.MaintainWeight, 2500)]
    [DataRow(FitnessGoal.ImproveEndurance, 2500)]
    [DataRow(FitnessGoal.ImproveFlexibility, 2500)]
    [DataRow(FitnessGoal.GeneralWellbeing, 2500)]
    public void SuggestCalorieTarget_EachGoal_AdjustsTdee(FitnessGoal goal, int expected)
    {
        Assert.AreEqual(expected, HealthCalculator.SuggestCalorieTarget(2500m, goal));
    }

    /// <summary>The suggestion never goes below 1,200 kcal.</summary>
    [TestMethod]
    [DataRow(1500.0, FitnessGoal.LoseWeight)]
    [DataRow(1100.0, FitnessGoal.MaintainWeight)]
    public void SuggestCalorieTarget_BelowFloor_Returns1200(double tdee, FitnessGoal goal)
    {
        Assert.AreEqual(1200, HealthCalculator.SuggestCalorieTarget((decimal)tdee, goal));
    }

    /// <summary>Exactly 1,200 kcal is allowed as it is.</summary>
    [TestMethod]
    public void SuggestCalorieTarget_ExactlyAtFloor_Returns1200()
    {
        Assert.AreEqual(1200, HealthCalculator.SuggestCalorieTarget(1700m, FitnessGoal.LoseWeight));
    }

    /// <summary>The target is rounded to whole kcal, halves away from zero.</summary>
    [TestMethod]
    public void SuggestCalorieTarget_HalfKcal_RoundsUp()
    {
        Assert.AreEqual(2063, HealthCalculator.SuggestCalorieTarget(2062.5m, FitnessGoal.MaintainWeight));
    }

    /// <summary>MET × kg × minutes / 60, to 2 decimal places.</summary>
    [TestMethod]
    [DataRow(8.0, 70.0, 30, 280.0)]
    [DataRow(3.5, 82.4, 45, 216.3)]
    [DataRow(9.8, 65.3, 20, 213.31)]
    public void CaloriesBurned_MetWeightAndMinutes_ReturnsKcal(double met, double weightKg, int minutes, double expected)
    {
        Assert.AreEqual((decimal)expected, HealthCalculator.CaloriesBurned((decimal)met, (decimal)weightKg, minutes));
    }
}
