using System.ComponentModel.DataAnnotations;
using HealthTrack.Helpers;
using HealthTrack.Models.Enums;
using HealthTrack.Models.ViewModels;
using HealthTrack.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace HealthTrack.Tests.ViewModels;

/// <summary>
/// Boundary checks of spec 6.4 on the forms, run the way MVC validates them on the server: the last allowed value passes and
/// the first value beyond it fails with a clear message. The clock reads Sunday 25 October 2026, 09:00.
/// </summary>
[TestClass]
public sealed class BoundaryValidationTests
{
    private static readonly IServiceProvider Services =
        new ServiceCollection().AddSingleton<IClock>(new FixedClock(TestHost.DefaultNow)).BuildServiceProvider();

    private static List<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model, Services, null), results, validateAllProperties: true);
        return results;
    }

    private static MeasurementViewModel Measurement(decimal weightKg, DateTime? measuredAt = null, decimal? glucose = null) => new()
    {
        MeasuredAt = measuredAt ?? TestHost.DefaultNow,
        WeightKg = weightKg,
        HeightCm = 170m,
        BloodGlucose = glucose
    };

    /// <summary>Spec 2.6: weight 20–350 kg.</summary>
    [TestMethod]
    [DataRow("19.9", false)]
    [DataRow("20", true)]
    [DataRow("350", true)]
    [DataRow("350.1", false)]
    public void Measurement_WeightAtTheEdges_OnlyTheRangeIsValid(string weight, bool valid)
    {
        var errors = Validate(Measurement(decimal.Parse(weight, System.Globalization.CultureInfo.InvariantCulture)));

        Assert.AreEqual(valid, errors.Count == 0, string.Join(" ", errors.Select(e => e.ErrorMessage)));
        if (!valid)
        {
            CollectionAssert.Contains(errors.Single().MemberNames.ToArray(), nameof(MeasurementViewModel.WeightKg));
        }
    }

    /// <summary>BR-13: now is fine, a minute from now is in the future.</summary>
    [TestMethod]
    public void Measurement_FutureTime_Refused()
    {
        Assert.IsEmpty(Validate(Measurement(70m, TestHost.DefaultNow)));
        Assert.AreEqual(nameof(MeasurementViewModel.MeasuredAt),
            Validate(Measurement(70m, TestHost.DefaultNow.AddMinutes(1))).Single().MemberNames.Single());
    }

    /// <summary>Spec 2.6: blood glucose 20–600 mg/dL (a value typed in mmol/L, such as 6.5, is caught).</summary>
    [TestMethod]
    public void Measurement_GlucoseInMmolPerLitre_Refused()
    {
        Assert.IsEmpty(Validate(Measurement(70m, glucose: 120m)));
        Assert.HasCount(1, Validate(Measurement(70m, glucose: 6.5m)));
    }

    /// <summary>Spec 2.6: a logged food is 0.25 to 20 servings.</summary>
    [TestMethod]
    [DataRow("0.24", false)]
    [DataRow("0.25", true)]
    [DataRow("20", true)]
    [DataRow("20.25", false)]
    public void MealItem_ServingsAtTheEdges_OnlyTheRangeIsValid(string servings, bool valid)
    {
        var item = new LogMealItemViewModel { FoodItemId = 1, Quantity = decimal.Parse(servings, System.Globalization.CultureInfo.InvariantCulture) };

        Assert.AreEqual(valid, Validate(item).Count == 0);
    }

    /// <summary>Spec 2.5: a logged exercise lasts 1–300 minutes, with up to 20 sets of up to 100 reps.</summary>
    [TestMethod]
    [DataRow(0, 3, 10, false)]
    [DataRow(1, 3, 10, true)]
    [DataRow(300, 20, 100, true)]
    [DataRow(301, 3, 10, false)]
    [DataRow(30, 21, 10, false)]
    [DataRow(30, 3, 101, false)]
    public void WorkoutEntry_AtTheEdges_OnlyTheRangeIsValid(int minutes, int sets, int reps, bool valid)
    {
        var entry = new LogEntryViewModel { Include = true, ExerciseId = 1, DurationMinutes = minutes, Sets = sets, Reps = reps };

        Assert.AreEqual(valid, Validate(entry).Count == 0);
    }

    /// <summary>Spec 1.6: appointments last 30, 45, 60 or 90 minutes; anything else is refused with that message.</summary>
    [TestMethod]
    [DataRow(45, true)]
    [DataRow(50, false)]
    [DataRow(120, false)]
    public void Booking_Length_OnlyAllowedLengths(int minutes, bool valid)
    {
        var booking = new BookAppointmentViewModel
        {
            TrainerId = 1,
            AppointmentType = AppointmentType.PersonalTraining,
            Mode = AppointmentMode.InPerson,
            Date = TestHost.DefaultNow.Date.AddDays(1),
            StartTime = TimeSpan.FromHours(10),
            DurationMinutes = minutes
        };

        var errors = Validate(booking);

        Assert.AreEqual(valid, errors.Count == 0);
        if (!valid)
        {
            Assert.AreEqual("Appointments last 30, 45, 60 or 90 minutes.", errors.Single().ErrorMessage);
        }
    }
}
