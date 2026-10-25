using System.ComponentModel.DataAnnotations;
using HealthTrack.Models.ViewModels;

namespace HealthTrack.Tests.ViewModels;

/// <summary>
/// The progress range (4 weeks, 3 months or custom, spec 1.7) and the measurement form's own rule.
/// </summary>
[TestClass]
public sealed class ProgressRangeViewModelTests
{
    private static readonly DateTime Today = new(2026, 10, 25);

    /// <summary>No or an unknown range gives the last 4 weeks, today included.</summary>
    [TestMethod]
    [DataRow(null)]
    [DataRow("nonsense")]
    [DataRow("4w")]
    public void Resolve_DefaultOrUnknown_LastFourWeeks(string? range)
    {
        var resolved = ProgressRangeViewModel.Resolve(range, null, null, Today);

        Assert.AreEqual(ProgressRangeViewModel.FourWeeks, resolved.Range);
        Assert.AreEqual(new DateTime(2026, 9, 28), resolved.From);
        Assert.AreEqual(Today, resolved.To);
    }

    /// <summary>3 months runs from the day after the same date three months ago.</summary>
    [TestMethod]
    public void Resolve_ThreeMonths_FromThreeMonthsAgo()
    {
        var resolved = ProgressRangeViewModel.Resolve("3m", null, null, Today);

        Assert.AreEqual(new DateTime(2026, 7, 26), resolved.From);
        Assert.AreEqual(Today, resolved.To);
    }

    /// <summary>Custom dates are put in order and can't end after today.</summary>
    [TestMethod]
    public void Resolve_CustomReversedAndFuture_OrderedAndClamped()
    {
        var resolved = ProgressRangeViewModel.Resolve("custom", new DateTime(2026, 12, 1), new DateTime(2026, 9, 1), Today);

        Assert.AreEqual(ProgressRangeViewModel.Custom, resolved.Range);
        Assert.AreEqual(new DateTime(2026, 9, 1), resolved.From);
        Assert.AreEqual(Today, resolved.To);
    }

    /// <summary>A custom range spans at most two years.</summary>
    [TestMethod]
    public void Resolve_CustomTooLong_LimitedToTwoYears()
    {
        var resolved = ProgressRangeViewModel.Resolve("custom", new DateTime(2020, 1, 1), new DateTime(2026, 10, 1), Today);

        Assert.AreEqual(new DateTime(2024, 10, 1), resolved.From);
    }

    /// <summary>
    /// Crafted extreme dates (the ones that caused a 500 in Phase 9's Production check) are clamped to the last ten years up
    /// to today instead of overflowing.
    /// </summary>
    [TestMethod]
    public void Resolve_CustomExtremeDates_ClampedWithoutError()
    {
        var ancient = ProgressRangeViewModel.Resolve("custom", DateTime.MinValue, DateTime.MinValue.AddDays(1), Today);
        var endless = ProgressRangeViewModel.Resolve("custom", DateTime.MinValue, DateTime.MaxValue, Today);

        Assert.AreEqual(Today.AddYears(-10), ancient.From);
        Assert.AreEqual(Today.AddYears(-10), ancient.To);
        Assert.AreEqual(Today.AddYears(-2), endless.From);
        Assert.AreEqual(Today, endless.To);
    }

    /// <summary>Custom without both dates falls back to 4 weeks.</summary>
    [TestMethod]
    public void Resolve_CustomMissingDate_FallsBackToFourWeeks()
    {
        Assert.AreEqual(ProgressRangeViewModel.FourWeeks, ProgressRangeViewModel.Resolve("custom", new DateTime(2026, 9, 1), null, Today).Range);
    }

    /// <summary>Spec 2.6: the diastolic pressure must be below the systolic.</summary>
    [TestMethod]
    public void MeasurementValidate_DiastolicNotBelowSystolic_ReturnsError()
    {
        var model = new MeasurementViewModel { MeasuredAt = Today, WeightKg = 70m, HeightCm = 170m, SystolicBp = 120, DiastolicBp = 120 };

        var error = model.Validate(new ValidationContext(model)).Single();

        CollectionAssert.AreEqual(new[] { nameof(MeasurementViewModel.DiastolicBp) }, error.MemberNames.ToArray());
        model.DiastolicBp = 80;
        Assert.IsEmpty(model.Validate(new ValidationContext(model)));
    }
}
