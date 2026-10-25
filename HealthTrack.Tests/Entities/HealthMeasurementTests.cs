using HealthTrack.Models.Entities;
using HealthTrack.Models.Enums;

namespace HealthTrack.Tests.Entities;

/// <summary>
/// BMI is computed from the readings, never typed (BR-14).
/// </summary>
[TestClass]
public sealed class HealthMeasurementTests
{
    /// <summary>The BMI and its category follow the weight and height.</summary>
    [TestMethod]
    [DataRow(95.0, 180.0, 29.3, BmiCategory.Overweight)]
    [DataRow(70.0, 175.0, 22.9, BmiCategory.Normal)]
    [DataRow(55.0, 175.0, 18.0, BmiCategory.Underweight)]
    [DataRow(102.5, 180.0, 31.6, BmiCategory.Obese)]
    public void Bmi_WeightAndHeight_ComputedWithCategory(double weightKg, double heightCm, double expectedBmi, BmiCategory expectedCategory)
    {
        var measurement = new HealthMeasurement { WeightKg = (decimal)weightKg, HeightCm = (decimal)heightCm };

        Assert.AreEqual((decimal)expectedBmi, measurement.Bmi);
        Assert.AreEqual(expectedCategory, measurement.BmiCategory);
    }
}
