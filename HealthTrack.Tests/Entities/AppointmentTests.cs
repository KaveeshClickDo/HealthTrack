using System.Globalization;
using HealthTrack.Models.Entities;
using HealthTrack.Models.Enums;
using HealthTrack.Tests.TestSupport;

namespace HealthTrack.Tests.Entities;

/// <summary>
/// The appointment lifecycle of spec 1.6 (BR-16, BR-17) and the half-open overlap check (BR-15).
/// </summary>
[TestClass]
public sealed class AppointmentTests
{
    // Monday 2 November 2026, 10:00–11:00.
    private static readonly DateTime Start = new(2026, 11, 2, 10, 0, 0);

    private static Appointment NewAppointment(AppointmentStatus status) =>
        new() { StartDateTime = Start, DurationMinutes = 60, Status = status };

    private static DateTime At(string time) => Start.Date + TimeSpan.Parse(time, CultureInfo.InvariantCulture);

    /// <summary>The end is the start plus the length.</summary>
    [TestMethod]
    public void EndDateTime_SixtyMinutes_EndsAnHourAfterStart()
    {
        Assert.AreEqual(new DateTime(2026, 11, 2, 11, 0, 0), NewAppointment(AppointmentStatus.Approved).EndDateTime);
    }

    /// <summary>A new appointment starts as Pending.</summary>
    [TestMethod]
    public void Status_NewAppointment_IsPending()
    {
        Assert.AreEqual(AppointmentStatus.Pending, new Appointment().Status);
    }

    /// <summary>Half-open intervals: touching ranges don't overlap, any shared minute does.</summary>
    [TestMethod]
    [DataRow("09:00", "10:00", false)]
    [DataRow("11:00", "12:00", false)]
    [DataRow("08:00", "09:00", false)]
    [DataRow("09:30", "10:30", true)]
    [DataRow("10:30", "11:30", true)]
    [DataRow("10:15", "10:45", true)]
    [DataRow("09:00", "12:00", true)]
    [DataRow("10:00", "11:00", true)]
    [DataRow("10:59", "11:30", true)]
    public void OverlapsWith_OtherRange_DetectsSharedTime(string from, string to, bool expected)
    {
        Assert.AreEqual(expected, NewAppointment(AppointmentStatus.Approved).OverlapsWith(At(from), At(to)));
    }

    /// <summary>Pending → Approved.</summary>
    [TestMethod]
    public void Approve_Pending_BecomesApproved()
    {
        var appointment = NewAppointment(AppointmentStatus.Pending);

        appointment.Approve();

        Assert.AreEqual(AppointmentStatus.Approved, appointment.Status);
    }

    /// <summary>Only a pending request can be approved, and a refusal leaves the status alone.</summary>
    [TestMethod]
    [DataRow(AppointmentStatus.Approved)]
    [DataRow(AppointmentStatus.Rescheduled)]
    [DataRow(AppointmentStatus.Rejected)]
    [DataRow(AppointmentStatus.CancelledByClient)]
    [DataRow(AppointmentStatus.CancelledByTrainer)]
    [DataRow(AppointmentStatus.Completed)]
    [DataRow(AppointmentStatus.NoShow)]
    public void Approve_NotPending_ThrowsBr16(AppointmentStatus status)
    {
        var appointment = NewAppointment(status);

        RuleAssert.Broken("BR-16", appointment.Approve);

        Assert.AreEqual(status, appointment.Status);
    }

    /// <summary>Pending → Rejected, with the reason stored trimmed.</summary>
    [TestMethod]
    public void Reject_PendingWithReason_BecomesRejectedAndStoresReason()
    {
        var appointment = NewAppointment(AppointmentStatus.Pending);

        appointment.Reject("  Fully booked that week  ");

        Assert.AreEqual(AppointmentStatus.Rejected, appointment.Status);
        Assert.AreEqual("Fully booked that week", appointment.CancellationReason);
    }

    /// <summary>A rejection needs a reason.</summary>
    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    public void Reject_BlankReason_ThrowsBr16(string reason)
    {
        var appointment = NewAppointment(AppointmentStatus.Pending);

        RuleAssert.Broken("BR-16", () => appointment.Reject(reason));

        Assert.AreEqual(AppointmentStatus.Pending, appointment.Status);
    }

    /// <summary>Only a pending request can be rejected.</summary>
    [TestMethod]
    [DataRow(AppointmentStatus.Approved)]
    [DataRow(AppointmentStatus.Rescheduled)]
    [DataRow(AppointmentStatus.Completed)]
    [DataRow(AppointmentStatus.Rejected)]
    public void Reject_NotPending_ThrowsBr16(AppointmentStatus status)
    {
        RuleAssert.Broken("BR-16", () => NewAppointment(status).Reject("Reason"));
    }

    /// <summary>Approved or Rescheduled → Rescheduled, at the new time and length.</summary>
    [TestMethod]
    [DataRow(AppointmentStatus.Approved)]
    [DataRow(AppointmentStatus.Rescheduled)]
    public void Reschedule_Confirmed_MovesAndBecomesRescheduled(AppointmentStatus status)
    {
        var appointment = NewAppointment(status);
        var newStart = Start.AddDays(1).AddHours(4);

        appointment.Reschedule(newStart, 45);

        Assert.AreEqual(AppointmentStatus.Rescheduled, appointment.Status);
        Assert.AreEqual(newStart, appointment.StartDateTime);
        Assert.AreEqual(45, appointment.DurationMinutes);
    }

    /// <summary>Pending and final appointments can't be rescheduled.</summary>
    [TestMethod]
    [DataRow(AppointmentStatus.Pending)]
    [DataRow(AppointmentStatus.Rejected)]
    [DataRow(AppointmentStatus.CancelledByClient)]
    [DataRow(AppointmentStatus.CancelledByTrainer)]
    [DataRow(AppointmentStatus.Completed)]
    [DataRow(AppointmentStatus.NoShow)]
    public void Reschedule_NotConfirmed_ThrowsBr16(AppointmentStatus status)
    {
        var appointment = NewAppointment(status);

        RuleAssert.Broken("BR-16", () => appointment.Reschedule(Start.AddDays(1), 60));

        Assert.AreEqual(Start, appointment.StartDateTime);
    }

    /// <summary>Appointments last 30, 45, 60 or 90 minutes.</summary>
    [TestMethod]
    [DataRow(0)]
    [DataRow(50)]
    [DataRow(120)]
    public void Reschedule_LengthNotAllowed_ThrowsBr15(int minutes)
    {
        RuleAssert.Broken("BR-15", () => NewAppointment(AppointmentStatus.Approved).Reschedule(Start.AddDays(1), minutes));
    }

    /// <summary>The client can cancel any open appointment more than 2 hours ahead.</summary>
    [TestMethod]
    [DataRow(AppointmentStatus.Pending)]
    [DataRow(AppointmentStatus.Approved)]
    [DataRow(AppointmentStatus.Rescheduled)]
    public void CancelByClient_OpenAndThreeHoursAhead_BecomesCancelledByClient(AppointmentStatus status)
    {
        var appointment = NewAppointment(status);

        appointment.CancelByClient("Feeling unwell", Start.AddHours(-3));

        Assert.AreEqual(AppointmentStatus.CancelledByClient, appointment.Status);
        Assert.AreEqual("Feeling unwell", appointment.CancellationReason);
    }

    /// <summary>BR-17: exactly 2 hours before the start is still allowed.</summary>
    [TestMethod]
    public void CancelByClient_ExactlyTwoHoursAhead_IsAllowed()
    {
        var appointment = NewAppointment(AppointmentStatus.Approved);

        appointment.CancelByClient(null, Start.AddHours(-2));

        Assert.AreEqual(AppointmentStatus.CancelledByClient, appointment.Status);
        Assert.IsNull(appointment.CancellationReason);
    }

    /// <summary>BR-17: inside the last 2 hours, and after the start, the client can't cancel.</summary>
    [TestMethod]
    [DataRow(-119)]
    [DataRow(-30)]
    [DataRow(0)]
    [DataRow(15)]
    public void CancelByClient_LessThanTwoHoursAhead_ThrowsBr17(int minutesFromStart)
    {
        var appointment = NewAppointment(AppointmentStatus.Approved);

        RuleAssert.Broken("BR-17", () => appointment.CancelByClient("Late", Start.AddMinutes(minutesFromStart)));

        Assert.AreEqual(AppointmentStatus.Approved, appointment.Status);
    }

    /// <summary>Final appointments can't be cancelled.</summary>
    [TestMethod]
    [DataRow(AppointmentStatus.Rejected)]
    [DataRow(AppointmentStatus.CancelledByClient)]
    [DataRow(AppointmentStatus.CancelledByTrainer)]
    [DataRow(AppointmentStatus.Completed)]
    [DataRow(AppointmentStatus.NoShow)]
    public void CancelByClient_Final_ThrowsBr16(AppointmentStatus status)
    {
        RuleAssert.Broken("BR-16", () => NewAppointment(status).CancelByClient(null, Start.AddDays(-1)));
    }

    /// <summary>The professional can cancel a confirmed appointment before its start, with a reason.</summary>
    [TestMethod]
    [DataRow(AppointmentStatus.Approved)]
    [DataRow(AppointmentStatus.Rescheduled)]
    public void CancelByTrainer_ConfirmedBeforeStartWithReason_BecomesCancelledByTrainer(AppointmentStatus status)
    {
        var appointment = NewAppointment(status);

        appointment.CancelByTrainer("Trainer ill", Start.AddMinutes(-1));

        Assert.AreEqual(AppointmentStatus.CancelledByTrainer, appointment.Status);
        Assert.AreEqual("Trainer ill", appointment.CancellationReason);
    }

    /// <summary>A trainer cancellation needs a reason.</summary>
    [TestMethod]
    public void CancelByTrainer_BlankReason_ThrowsBr16()
    {
        var appointment = NewAppointment(AppointmentStatus.Approved);

        RuleAssert.Broken("BR-16", () => appointment.CancelByTrainer(" ", Start.AddDays(-1)));

        Assert.AreEqual(AppointmentStatus.Approved, appointment.Status);
    }

    /// <summary>Pending requests are rejected, not cancelled, and final ones can't change.</summary>
    [TestMethod]
    [DataRow(AppointmentStatus.Pending)]
    [DataRow(AppointmentStatus.Rejected)]
    [DataRow(AppointmentStatus.CancelledByClient)]
    [DataRow(AppointmentStatus.Completed)]
    [DataRow(AppointmentStatus.NoShow)]
    public void CancelByTrainer_NotConfirmed_ThrowsBr16(AppointmentStatus status)
    {
        RuleAssert.Broken("BR-16", () => NewAppointment(status).CancelByTrainer("Reason", Start.AddDays(-1)));
    }

    /// <summary>Once the appointment has started the professional can't cancel it.</summary>
    [TestMethod]
    [DataRow(0)]
    [DataRow(30)]
    public void CancelByTrainer_AtOrAfterStart_ThrowsBr17(int minutesFromStart)
    {
        RuleAssert.Broken("BR-17",
            () => NewAppointment(AppointmentStatus.Approved).CancelByTrainer("Reason", Start.AddMinutes(minutesFromStart)));
    }

    /// <summary>From the start time on, a confirmed appointment can be completed with notes.</summary>
    [TestMethod]
    [DataRow(AppointmentStatus.Approved, 0)]
    [DataRow(AppointmentStatus.Rescheduled, 75)]
    public void Complete_ConfirmedAfterStart_BecomesCompletedWithNotes(AppointmentStatus status, int minutesFromStart)
    {
        var appointment = NewAppointment(status);

        appointment.Complete(" Good progress on mobility. ", Start.AddMinutes(minutesFromStart));

        Assert.AreEqual(AppointmentStatus.Completed, appointment.Status);
        Assert.AreEqual("Good progress on mobility.", appointment.ProfessionalNotes);
    }

    /// <summary>BR-17: an appointment can't be completed before it starts.</summary>
    [TestMethod]
    public void Complete_BeforeStart_ThrowsBr17()
    {
        var appointment = NewAppointment(AppointmentStatus.Approved);

        RuleAssert.Broken("BR-17", () => appointment.Complete("Notes", Start.AddMinutes(-1)));

        Assert.AreEqual(AppointmentStatus.Approved, appointment.Status);
    }

    /// <summary>Only a confirmed appointment can be completed.</summary>
    [TestMethod]
    [DataRow(AppointmentStatus.Pending)]
    [DataRow(AppointmentStatus.Rejected)]
    [DataRow(AppointmentStatus.CancelledByTrainer)]
    [DataRow(AppointmentStatus.Completed)]
    [DataRow(AppointmentStatus.NoShow)]
    public void Complete_NotConfirmed_ThrowsBr16(AppointmentStatus status)
    {
        RuleAssert.Broken("BR-16", () => NewAppointment(status).Complete("Notes", Start.AddHours(1)));
    }

    /// <summary>From the start time on, a confirmed appointment can be marked as a no-show.</summary>
    [TestMethod]
    [DataRow(AppointmentStatus.Approved)]
    [DataRow(AppointmentStatus.Rescheduled)]
    public void MarkNoShow_ConfirmedAfterStart_BecomesNoShow(AppointmentStatus status)
    {
        var appointment = NewAppointment(status);

        appointment.MarkNoShow(Start.AddMinutes(20));

        Assert.AreEqual(AppointmentStatus.NoShow, appointment.Status);
    }

    /// <summary>BR-17: a no-show can't be recorded before the start.</summary>
    [TestMethod]
    public void MarkNoShow_BeforeStart_ThrowsBr17()
    {
        RuleAssert.Broken("BR-17", () => NewAppointment(AppointmentStatus.Approved).MarkNoShow(Start.AddMinutes(-5)));
    }

    /// <summary>Only a confirmed appointment can be marked as a no-show.</summary>
    [TestMethod]
    [DataRow(AppointmentStatus.Pending)]
    [DataRow(AppointmentStatus.CancelledByClient)]
    [DataRow(AppointmentStatus.Completed)]
    public void MarkNoShow_NotConfirmed_ThrowsBr16(AppointmentStatus status)
    {
        RuleAssert.Broken("BR-16", () => NewAppointment(status).MarkNoShow(Start.AddHours(1)));
    }
}
