using HealthTrack.Models.Entities;
using HealthTrack.Models.Enums;
using HealthTrack.Models.ViewModels;
using HealthTrack.Services;
using HealthTrack.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace HealthTrack.Tests.Services;

/// <summary>
/// Free slots, booking and the appointment lifecycle (spec 1.6, BR-15 to BR-18). The test clock reads Sunday
/// 25 October 2026, 09:00, so Monday 26 October is tomorrow.
/// </summary>
[TestClass]
public sealed class AppointmentServiceTests
{
    private static readonly DateTime Monday = new(2026, 10, 26);

    private static async Task<(TestHost Host, TrainerProfile Trainer, ClientProfile Client)> ArrangeAsync(IInterceptor? interceptor = null)
    {
        var host = await TestHost.CreateAsync(interceptor: interceptor);
        var trainer = await host.CreateTrainerAsync("trainer@example.test", firstName: "Chris");
        var client = await host.CreateClientAsync("client@example.test", firstName: "Amaya");
        await host.AddAvailabilityAsync(trainer, DayOfWeek.Monday, 9, 12);
        return (host, trainer, client);
    }

    private static async Task<string> VersionAsync(TestHost host, int appointmentId) =>
        Convert.ToBase64String((await host.Db.Appointments.AsNoTracking().SingleAsync(a => a.AppointmentId == appointmentId)).RowVersion);

    private static BookAppointmentViewModel Booking(TrainerProfile trainer, DateTime start, int minutes = 60) => new()
    {
        TrainerId = trainer.TrainerId,
        AppointmentType = AppointmentType.FitnessAssessment,
        Mode = AppointmentMode.Online,
        Date = start.Date,
        StartTime = start.TimeOfDay,
        DurationMinutes = minutes,
        ClientReason = "  Sore lower back after running  "
    };

    private static string[] Times(IEnumerable<DateTime> slots) => slots.Select(s => s.ToString("HH:mm")).ToArray();

    /// <summary>Starts every 15 minutes, and only where the whole appointment fits inside the window.</summary>
    [TestMethod]
    [DataRow(60, new[] { "09:00", "09:15", "09:30", "09:45", "10:00", "10:15", "10:30", "10:45", "11:00" })]
    [DataRow(90, new[] { "09:00", "09:15", "09:30", "09:45", "10:00", "10:15", "10:30" })]
    public async Task GetAvailableSlotsAsync_OneWindow_EveryQuarterHourThatFits(int minutes, string[] expected)
    {
        var (host, trainer, client) = await ArrangeAsync();
        await using var _ = host;

        var slots = await host.Get<IAppointmentService>().GetAvailableSlotsAsync(trainer.TrainerId, client.ClientId, Monday, minutes);

        CollectionAssert.AreEqual(expected, Times(slots));
    }

    /// <summary>Two windows give two runs of times; a day without a window gives none.</summary>
    [TestMethod]
    public async Task GetAvailableSlotsAsync_TwoWindowsAndAnEmptyDay_RespectsWindows()
    {
        var (host, trainer, client) = await ArrangeAsync();
        await using var _ = host;
        await host.AddAvailabilityAsync(trainer, DayOfWeek.Monday, 14, 15);
        var appointments = host.Get<IAppointmentService>();

        var monday = await appointments.GetAvailableSlotsAsync(trainer.TrainerId, client.ClientId, Monday, 30);
        var tuesday = await appointments.GetAvailableSlotsAsync(trainer.TrainerId, client.ClientId, Monday.AddDays(1), 30);

        CollectionAssert.IsSubsetOf(new[] { "11:30", "14:00", "14:30" }, Times(monday));
        CollectionAssert.DoesNotContain(Times(monday), "14:45");
        CollectionAssert.DoesNotContain(Times(monday), "12:00");
        Assert.IsEmpty(tuesday);
    }

    /// <summary>BR-15: nothing earlier than 2 hours from now (09:00 now, so 11:00 is the first time today).</summary>
    [TestMethod]
    public async Task GetAvailableSlotsAsync_Today_StartsTwoHoursFromNow()
    {
        var (host, trainer, client) = await ArrangeAsync();
        await using var _ = host;
        await host.AddAvailabilityAsync(trainer, DayOfWeek.Sunday, 9, 13);

        var slots = await host.Get<IAppointmentService>().GetAvailableSlotsAsync(trainer.TrainerId, client.ClientId, host.Clock.Today, 60);

        CollectionAssert.AreEqual(new[] { "11:00", "11:15", "11:30", "11:45", "12:00" }, Times(slots));
    }

    /// <summary>
    /// BR-15: a day in the past or beyond 60 days has no free times, including a crafted 9999-12-31 (which must not
    /// overflow the date arithmetic and end on the error page).
    /// </summary>
    [TestMethod]
    public async Task GetAvailableSlotsAsync_OutsideBookingWindow_NoSlotsAndNoError()
    {
        var (host, trainer, client) = await ArrangeAsync();
        await using var _ = host;
        await host.AddAvailabilityAsync(trainer, DayOfWeek.Friday, 9, 12);
        var appointments = host.Get<IAppointmentService>();

        Assert.IsEmpty(await appointments.GetAvailableSlotsAsync(trainer.TrainerId, client.ClientId, DateTime.MaxValue.Date, 60));
        Assert.IsEmpty(await appointments.GetAvailableSlotsAsync(trainer.TrainerId, client.ClientId, Monday.AddDays(-7), 60));
    }

    /// <summary>BR-15: up to 60 days ahead (Thursday 24 December, 09:00 exactly), and nothing after that.</summary>
    [TestMethod]
    public async Task GetAvailableSlotsAsync_SixtyDaysAhead_LastSlotIsExactlySixtyDays()
    {
        var (host, trainer, client) = await ArrangeAsync();
        await using var _ = host;
        await host.AddAvailabilityAsync(trainer, DayOfWeek.Thursday, 9, 12);
        var appointments = host.Get<IAppointmentService>();

        var lastDay = await appointments.GetAvailableSlotsAsync(trainer.TrainerId, client.ClientId, new DateTime(2026, 12, 24), 60);
        var weekLater = await appointments.GetAvailableSlotsAsync(trainer.TrainerId, client.ClientId, new DateTime(2026, 12, 31), 60);

        CollectionAssert.AreEqual(new[] { "09:00" }, Times(lastDay));
        Assert.IsEmpty(weekLater);
    }

    /// <summary>
    /// BR-15: the trainer's open appointment (10:00–11:00) takes every overlapping start; ending at 10:00 or starting at
    /// 11:00 is fine, and a cancelled appointment doesn't hold its slot.
    /// </summary>
    [TestMethod]
    public async Task GetAvailableSlotsAsync_TrainerBooked_LeavesOutOverlaps()
    {
        var (host, trainer, client) = await ArrangeAsync();
        await using var _ = host;
        var other = await host.CreateClientAsync("other@example.test");
        await host.AddAppointmentAsync(other, trainer, Monday.AddHours(10));
        await host.AddAppointmentAsync(other, trainer, Monday.AddHours(9), status: AppointmentStatus.CancelledByClient);

        var slots = await host.Get<IAppointmentService>().GetAvailableSlotsAsync(trainer.TrainerId, client.ClientId, Monday, 60);

        CollectionAssert.AreEqual(new[] { "09:00", "11:00" }, Times(slots));
    }

    /// <summary>BR-15: the client's own appointment with another professional also takes the time.</summary>
    [TestMethod]
    public async Task GetAvailableSlotsAsync_ClientBookedElsewhere_LeavesOutOverlaps()
    {
        var (host, trainer, client) = await ArrangeAsync();
        await using var _ = host;
        var nutritionist = await host.CreateTrainerAsync("nutritionist@example.test");
        await host.AddAppointmentAsync(client, nutritionist, Monday.AddHours(9).AddMinutes(30), minutes: 30, status: AppointmentStatus.Pending);

        var slots = await host.Get<IAppointmentService>().GetAvailableSlotsAsync(trainer.TrainerId, client.ClientId, Monday, 60);

        Assert.AreEqual("10:00", Times(slots)[0]);
        CollectionAssert.DoesNotContain(Times(slots), "09:00");
    }

    /// <summary>A professional who isn't approved, or whose account is suspended, offers no times.</summary>
    [TestMethod]
    public async Task GetAvailableSlotsAsync_UnapprovedOrSuspendedTrainer_NoSlots()
    {
        await using var host = await TestHost.CreateAsync();
        var pending = await host.CreateTrainerAsync("pending@example.test", approved: false);
        var suspended = await host.CreateTrainerAsync("suspended@example.test", status: AccountStatus.Suspended);
        var client = await host.CreateClientAsync("client@example.test");
        await host.AddAvailabilityAsync(pending, DayOfWeek.Monday, 9, 12);
        await host.AddAvailabilityAsync(suspended, DayOfWeek.Monday, 9, 12);
        var appointments = host.Get<IAppointmentService>();

        Assert.IsEmpty(await appointments.GetAvailableSlotsAsync(pending.TrainerId, client.ClientId, Monday, 60));
        Assert.IsEmpty(await appointments.GetAvailableSlotsAsync(suspended.TrainerId, client.ClientId, Monday, 60));
    }

    /// <summary>UC13: a booking is Pending for the signed-in client, the professional is notified with a link, and it's audited.</summary>
    [TestMethod]
    public async Task BookAsync_FreeSlot_CreatesPendingAndNotifiesTrainer()
    {
        var (host, trainer, client) = await ArrangeAsync();
        await using var _ = host;

        var id = await host.Get<IAppointmentService>().BookAsync(client.UserId, Booking(trainer, Monday.AddHours(10)));

        var appointment = await host.Db.Appointments.AsNoTracking().SingleAsync();
        Assert.AreEqual(appointment.AppointmentId, id);
        Assert.AreEqual(AppointmentStatus.Pending, appointment.Status);
        Assert.AreEqual(client.ClientId, appointment.ClientId);
        Assert.AreEqual(Monday.AddHours(10), appointment.StartDateTime);
        Assert.AreEqual("Sore lower back after running", appointment.ClientReason);

        var notification = await host.Db.Notifications.SingleAsync();
        Assert.AreEqual(trainer.UserId, notification.UserId);
        Assert.AreEqual(NotificationType.AppointmentUpdate, notification.Type);
        Assert.AreEqual($"/Trainer/Appointments/Details/{id}", notification.LinkUrl);
        var audit = await host.Db.AuditLogs.SingleAsync();
        Assert.AreEqual(AuditAction.Create, audit.Action);
        Assert.IsFalse(audit.Details!.Contains("back"));
    }

    /// <summary>BR-15: a time that overlaps another open appointment of the professional is refused.</summary>
    [TestMethod]
    public async Task BookAsync_OverlapsTrainersAppointment_ThrowsBr15()
    {
        var (host, trainer, client) = await ArrangeAsync();
        await using var _ = host;
        var other = await host.CreateClientAsync("other@example.test");
        await host.AddAppointmentAsync(other, trainer, Monday.AddHours(10), status: AppointmentStatus.Pending);

        await RuleAssert.BrokenAsync("BR-15", () => host.Get<IAppointmentService>().BookAsync(client.UserId, Booking(trainer, Monday.AddHours(9).AddMinutes(30))));
        Assert.AreEqual(1, await host.Db.Appointments.CountAsync());
    }

    /// <summary>BR-15: outside the windows, too soon, too far ahead or an odd length are all refused.</summary>
    [TestMethod]
    public async Task BookAsync_BadTimes_ThrowBr15()
    {
        var (host, trainer, client) = await ArrangeAsync();
        await using var _ = host;
        await host.AddAvailabilityAsync(trainer, DayOfWeek.Sunday, 9, 17);
        var appointments = host.Get<IAppointmentService>();

        await RuleAssert.BrokenAsync("BR-15", () => appointments.BookAsync(client.UserId, Booking(trainer, Monday.AddHours(11).AddMinutes(30))));
        await RuleAssert.BrokenAsync("BR-15", () => appointments.BookAsync(client.UserId, Booking(trainer, host.Clock.Now.AddMinutes(90))));
        await RuleAssert.BrokenAsync("BR-15", () => appointments.BookAsync(client.UserId, Booking(trainer, Monday.AddDays(63).AddHours(9))));
        await RuleAssert.BrokenAsync("BR-15", () => appointments.BookAsync(client.UserId, Booking(trainer, Monday.AddHours(9), minutes: 50)));
        Assert.IsFalse(await host.Db.Appointments.AnyAsync());
    }

    /// <summary>BR-03: a professional who isn't approved can't be booked, even inside a window.</summary>
    [TestMethod]
    public async Task BookAsync_UnapprovedTrainer_ThrowsBr03()
    {
        await using var host = await TestHost.CreateAsync();
        var pending = await host.CreateTrainerAsync("pending@example.test", approved: false);
        var client = await host.CreateClientAsync("client@example.test");
        await host.AddAvailabilityAsync(pending, DayOfWeek.Monday, 9, 12);

        await RuleAssert.BrokenAsync("BR-03", () => host.Get<IAppointmentService>().BookAsync(client.UserId, Booking(pending, Monday.AddHours(9))));
        Assert.IsFalse(await host.Db.Appointments.AnyAsync());
    }

    /// <summary>UC24: approving sets the status and the room, tells the client and is audited.</summary>
    [TestMethod]
    public async Task ApproveAsync_PendingRequest_ApprovesAndNotifiesClient()
    {
        var (host, trainer, client) = await ArrangeAsync();
        await using var _ = host;
        var appointment = await host.AddAppointmentAsync(client, trainer, Monday.AddHours(10), status: AppointmentStatus.Pending);

        Assert.IsTrue(await host.Get<IAppointmentService>().ApproveAsync(appointment.AppointmentId, trainer.UserId, " Studio 2 ",
            await VersionAsync(host, appointment.AppointmentId)));

        var stored = await host.Db.Appointments.AsNoTracking().SingleAsync();
        Assert.AreEqual(AppointmentStatus.Approved, stored.Status);
        Assert.AreEqual("Studio 2", stored.Location);
        Assert.AreEqual(host.Clock.Now, stored.UpdatedAt);
        var notification = await host.Db.Notifications.SingleAsync();
        Assert.AreEqual(client.UserId, notification.UserId);
        Assert.AreEqual($"/Client/Appointments/Details/{appointment.AppointmentId}", notification.LinkUrl);
        Assert.AreEqual(AuditAction.Approve, (await host.Db.AuditLogs.SingleAsync()).Action);
    }

    /// <summary>BR-16: a rejection needs a reason, and another professional can't touch the appointment.</summary>
    [TestMethod]
    public async Task RejectAsync_NoReasonOrOtherTrainer_Refused()
    {
        var (host, trainer, client) = await ArrangeAsync();
        await using var _ = host;
        var other = await host.CreateTrainerAsync("other@example.test");
        var appointment = await host.AddAppointmentAsync(client, trainer, Monday.AddHours(10), status: AppointmentStatus.Pending);
        var version = await VersionAsync(host, appointment.AppointmentId);
        var appointments = host.Get<IAppointmentService>();

        Assert.IsFalse(await appointments.RejectAsync(appointment.AppointmentId, other.UserId, "Busy", version));
        await RuleAssert.BrokenAsync("BR-16", () => appointments.RejectAsync(appointment.AppointmentId, trainer.UserId, "  ", version));
    }

    /// <summary>UC25: moving within its own slot (10:00 → 10:15) works, because the appointment's own time doesn't count.</summary>
    [TestMethod]
    public async Task RescheduleAsync_OverlapsOnlyItsOwnSlot_Succeeds()
    {
        var (host, trainer, client) = await ArrangeAsync();
        await using var _ = host;
        var appointment = await host.AddAppointmentAsync(client, trainer, Monday.AddHours(10));
        var appointments = host.Get<IAppointmentService>();

        var slots = await appointments.GetAvailableSlotsAsync(trainer.TrainerId, client.ClientId, Monday, 60, appointment.AppointmentId);
        CollectionAssert.Contains(Times(slots), "10:15");

        Assert.IsTrue(await appointments.RescheduleAsync(appointment.AppointmentId, trainer.UserId, Monday.AddHours(10).AddMinutes(15), 45,
            await VersionAsync(host, appointment.AppointmentId)));

        var stored = await host.Db.Appointments.AsNoTracking().SingleAsync();
        Assert.AreEqual(AppointmentStatus.Rescheduled, stored.Status);
        Assert.AreEqual(Monday.AddHours(10).AddMinutes(15), stored.StartDateTime);
        Assert.AreEqual(45, stored.DurationMinutes);
        Assert.AreEqual(client.UserId, (await host.Db.Notifications.SingleAsync()).UserId);
    }

    /// <summary>BR-15: rescheduling onto another of the professional's appointments is refused.</summary>
    [TestMethod]
    public async Task RescheduleAsync_OntoAnotherAppointment_ThrowsBr15()
    {
        var (host, trainer, client) = await ArrangeAsync();
        await using var _ = host;
        var other = await host.CreateClientAsync("other@example.test");
        var appointment = await host.AddAppointmentAsync(client, trainer, Monday.AddHours(9));
        await host.AddAppointmentAsync(other, trainer, Monday.AddHours(11));

        await RuleAssert.BrokenAsync("BR-15", () => host.Get<IAppointmentService>().RescheduleAsync(appointment.AppointmentId, trainer.UserId,
            Monday.AddHours(10).AddMinutes(30), 60, Convert.ToBase64String(appointment.RowVersion)));
    }

    /// <summary>BR-16: a pending request can't be rescheduled, only approved or rejected.</summary>
    [TestMethod]
    public async Task RescheduleAsync_Pending_ThrowsBr16()
    {
        var (host, trainer, client) = await ArrangeAsync();
        await using var _ = host;
        var appointment = await host.AddAppointmentAsync(client, trainer, Monday.AddHours(9), status: AppointmentStatus.Pending);

        await RuleAssert.BrokenAsync("BR-16", () => host.Get<IAppointmentService>().RescheduleAsync(appointment.AppointmentId, trainer.UserId,
            Monday.AddHours(10), 60, Convert.ToBase64String(appointment.RowVersion)));
    }

    /// <summary>BR-17: the client can't cancel within 2 hours of the start (10:30 today, now 09:00).</summary>
    [TestMethod]
    public async Task CancelAsync_ClientWithinTwoHours_ThrowsBr17()
    {
        var (host, trainer, client) = await ArrangeAsync();
        await using var _ = host;
        var appointment = await host.AddAppointmentAsync(client, trainer, host.Clock.Now.AddMinutes(90));

        await RuleAssert.BrokenAsync("BR-17", () => host.Get<IAppointmentService>().CancelAsync(appointment.AppointmentId, client.UserId,
            null, Convert.ToBase64String(appointment.RowVersion)));
        Assert.AreEqual(AppointmentStatus.Approved, (await host.Db.Appointments.AsNoTracking().SingleAsync()).Status);
    }

    /// <summary>The client cancels in time; the professional is told. The professional must give a reason (BR-16).</summary>
    [TestMethod]
    public async Task CancelAsync_ClientInTimeAndTrainerWithoutReason_MapsToTheRightRule()
    {
        var (host, trainer, client) = await ArrangeAsync();
        await using var _ = host;
        var mine = await host.AddAppointmentAsync(client, trainer, Monday.AddHours(9));
        var theirs = await host.AddAppointmentAsync(client, trainer, Monday.AddHours(11));
        var appointments = host.Get<IAppointmentService>();

        Assert.IsTrue(await appointments.CancelAsync(mine.AppointmentId, client.UserId, null, await VersionAsync(host, mine.AppointmentId)));
        await RuleAssert.BrokenAsync("BR-16", () => appointments.CancelAsync(theirs.AppointmentId, trainer.UserId, null, Convert.ToBase64String(theirs.RowVersion)));

        Assert.AreEqual(AppointmentStatus.CancelledByClient, (await host.Db.Appointments.AsNoTracking().SingleAsync(a => a.AppointmentId == mine.AppointmentId)).Status);
        Assert.AreEqual(trainer.UserId, (await host.Db.Notifications.SingleAsync()).UserId);
        Assert.AreEqual(AuditAction.Cancel, (await host.Db.AuditLogs.SingleAsync()).Action);
    }

    /// <summary>BR-17: an appointment can't be completed before it starts.</summary>
    [TestMethod]
    public async Task CompleteAsync_BeforeStart_ThrowsBr17()
    {
        var (host, trainer, client) = await ArrangeAsync();
        await using var _ = host;
        var appointment = await host.AddAppointmentAsync(client, trainer, Monday.AddHours(9));

        await RuleAssert.BrokenAsync("BR-17", () => host.Get<IAppointmentService>().CompleteAsync(appointment.AppointmentId, trainer.UserId,
            "Good session", Convert.ToBase64String(appointment.RowVersion)));
    }

    /// <summary>After the start, completing stores the notes and tells the client; the notes stay out of the audit row.</summary>
    [TestMethod]
    public async Task CompleteAsync_AfterStart_StoresNotes()
    {
        var (host, trainer, client) = await ArrangeAsync();
        await using var _ = host;
        var appointment = await host.AddAppointmentAsync(client, trainer, host.Clock.Now.AddHours(-1));

        Assert.IsTrue(await host.Get<IAppointmentService>().CompleteAsync(appointment.AppointmentId, trainer.UserId,
            "Hip mobility is limited; added stretches.", await VersionAsync(host, appointment.AppointmentId)));

        var stored = await host.Db.Appointments.AsNoTracking().SingleAsync();
        Assert.AreEqual(AppointmentStatus.Completed, stored.Status);
        Assert.AreEqual("Hip mobility is limited; added stretches.", stored.ProfessionalNotes);
        Assert.IsFalse((await host.Db.AuditLogs.SingleAsync()).Details!.Contains("Hip"));
    }

    /// <summary>
    /// A stale form (its RowVersion no longer matches) gets the concurrency message before any lifecycle rule runs,
    /// and nothing changes.
    /// </summary>
    [TestMethod]
    public async Task ApproveAsync_StaleRowVersion_ThrowsConcurrencyBeforeRules()
    {
        var (host, trainer, client) = await ArrangeAsync();
        await using var _ = host;
        var appointment = await host.AddAppointmentAsync(client, trainer, Monday.AddHours(10), status: AppointmentStatus.Rejected);
        appointment.RowVersion = [1, 2, 3, 4];
        await host.Db.SaveChangesAsync();

        // Rejected can't be approved, but the stale form is reported first.
        var error = await Assert.ThrowsExactlyAsync<DbUpdateConcurrencyException>(() =>
            host.Get<IAppointmentService>().ApproveAsync(appointment.AppointmentId, trainer.UserId, null, Convert.ToBase64String([9, 9, 9, 9])));

        Assert.AreEqual(AppointmentService.ConcurrencyMessage, error.Message);
        Assert.IsFalse(await host.Db.Notifications.AnyAsync());
    }

    /// <summary>BR-08, BR-20: the details, with the reason, are for the client and that professional only.</summary>
    [TestMethod]
    public async Task GetDetailsAsync_OnlyClientAndOwnProfessional()
    {
        var (host, trainer, client) = await ArrangeAsync();
        await using var _ = host;
        var other = await host.CreateTrainerAsync("other@example.test");
        var stranger = await host.CreateClientAsync("stranger@example.test");
        var appointment = await host.AddAppointmentAsync(client, trainer, Monday.AddHours(10), status: AppointmentStatus.Pending);
        var appointments = host.Get<IAppointmentService>();

        var forTrainer = await appointments.GetDetailsAsync(appointment.AppointmentId, trainer.UserId);
        var forClient = await appointments.GetDetailsAsync(appointment.AppointmentId, client.UserId);

        Assert.IsTrue(forTrainer!.ViewerIsTrainer);
        Assert.IsTrue(forTrainer.CanDecide);
        Assert.AreEqual("Knee feels stiff", forTrainer.Appointment.ClientReason);
        Assert.IsFalse(forClient!.ViewerIsTrainer);
        Assert.IsTrue(forClient.Appointment.CanCancel);
        Assert.IsNull(await appointments.GetDetailsAsync(appointment.AppointmentId, other.UserId));
        Assert.IsNull(await appointments.GetDetailsAsync(appointment.AppointmentId, stranger.UserId));
    }

    /// <summary>The trainer's page lists pending requests first, then today on, then earlier ones.</summary>
    [TestMethod]
    public async Task GetForTrainerAsync_MixedAppointments_GroupsThem()
    {
        var (host, trainer, client) = await ArrangeAsync();
        await using var _ = host;
        await host.AddAppointmentAsync(client, trainer, Monday.AddHours(11), status: AppointmentStatus.Pending);
        await host.AddAppointmentAsync(client, trainer, Monday.AddHours(9));
        await host.AddAppointmentAsync(client, trainer, host.Clock.Today.AddDays(-7).AddHours(9), status: AppointmentStatus.Completed);

        var page = await host.Get<IAppointmentService>().GetForTrainerAsync(trainer.UserId, 1);

        Assert.AreEqual(AppointmentStatus.Pending, page.Pending.Single().Status);
        Assert.AreEqual(AppointmentStatus.Approved, page.Upcoming.Single().Status);
        Assert.AreEqual(AppointmentStatus.Completed, page.Past.Items.Single().Status);
    }

    /// <summary>Spec 2.4: windows on one day can't overlap; one that starts when another ends is fine.</summary>
    [TestMethod]
    public async Task AddAvailabilityAsync_OverlappingWindow_ThrowsBr15()
    {
        var (host, trainer, _) = await ArrangeAsync();
        await using var __ = host;
        var appointments = host.Get<IAppointmentService>();

        await RuleAssert.BrokenAsync("BR-15", () => appointments.AddAvailabilityAsync(trainer.UserId,
            new AvailabilityViewModel { DayOfWeek = DayOfWeek.Monday, StartTime = TimeSpan.FromHours(11), EndTime = TimeSpan.FromHours(13) }));
        Assert.IsTrue(await appointments.AddAvailabilityAsync(trainer.UserId,
            new AvailabilityViewModel { DayOfWeek = DayOfWeek.Monday, StartTime = TimeSpan.FromHours(12), EndTime = TimeSpan.FromHours(13) }));

        var page = await appointments.GetAvailabilityAsync(trainer.UserId);
        CollectionAssert.AreEqual(new[] { TimeSpan.FromHours(9), TimeSpan.FromHours(12) }, page.Windows.Select(w => w.StartTime).ToArray());
        Assert.AreEqual(4d, page.WeeklyHours);
    }

    /// <summary>A trainer can only remove their own windows.</summary>
    [TestMethod]
    public async Task RemoveAvailabilityAsync_OtherTrainersWindow_ReturnsFalse()
    {
        var (host, trainer, _) = await ArrangeAsync();
        await using var __ = host;
        var other = await host.CreateTrainerAsync("other@example.test");
        var window = await host.Db.TrainerAvailabilities.SingleAsync();
        var appointments = host.Get<IAppointmentService>();

        Assert.IsFalse(await appointments.RemoveAvailabilityAsync(window.AvailabilityId, other.UserId));
        Assert.IsTrue(await appointments.RemoveAvailabilityAsync(window.AvailabilityId, trainer.UserId));
        Assert.IsFalse(await host.Db.TrainerAvailabilities.AnyAsync());
    }

    /// <summary>
    /// BR-15: another client books 10:00 at the same moment. SQL Server lets that booking through and rolls this one
    /// back to break the deadlock; the retry finds the time taken and says so, and nothing of this booking is left behind.
    /// </summary>
    [TestMethod]
    public async Task BookAsync_LosesDeadlockToSameTime_ThrowsSlotTakenMessage()
    {
        var deadlock = new SqlErrorInterceptor<Appointment>(1205);
        var (host, trainer, client) = await ArrangeAsync(deadlock);
        await using var _ = host;
        var other = await host.CreateClientAsync("other@example.test");
        deadlock.FailuresLeft = 1;
        deadlock.OtherRequest = async () =>
        {
            await using var scope = host.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IAppointmentService>().BookAsync(other.UserId, Booking(trainer, Monday.AddHours(10)));
        };

        var error = await RuleAssert.BrokenAsync("BR-15", () => host.Get<IAppointmentService>().BookAsync(client.UserId, Booking(trainer, Monday.AddHours(10))));

        Assert.AreEqual(AppointmentService.SlotTakenMessage, error.Message);
        Assert.AreEqual(other.ClientId, (await host.Db.Appointments.AsNoTracking().SingleAsync()).ClientId);
        Assert.IsFalse(host.Db.ChangeTracker.Entries<Appointment>().Any());
    }

    /// <summary>A lost deadlock against a booking that didn't clash (another time) just runs again and books.</summary>
    [TestMethod]
    public async Task BookAsync_LosesDeadlockWithoutClash_RetriesAndBooks()
    {
        var deadlock = new SqlErrorInterceptor<Appointment>(1205);
        var (host, trainer, client) = await ArrangeAsync(deadlock);
        await using var _ = host;
        deadlock.FailuresLeft = 1;

        var id = await host.Get<IAppointmentService>().BookAsync(client.UserId, Booking(trainer, Monday.AddHours(10)));

        Assert.AreEqual(1, deadlock.Failures);
        Assert.AreEqual(id, (await host.Db.Appointments.AsNoTracking().SingleAsync()).AppointmentId);
    }

    /// <summary>After three lost deadlocks in a row the booking gives up with the friendly message, not the error page.</summary>
    [TestMethod]
    public async Task BookAsync_DeadlockEveryTime_GivesUpWithSlotTakenMessage()
    {
        var deadlock = new SqlErrorInterceptor<Appointment>(1205);
        var (host, trainer, client) = await ArrangeAsync(deadlock);
        await using var _ = host;
        deadlock.FailuresLeft = int.MaxValue;

        var error = await RuleAssert.BrokenAsync("BR-15", () => host.Get<IAppointmentService>().BookAsync(client.UserId, Booking(trainer, Monday.AddHours(10))));

        Assert.AreEqual(AppointmentService.SlotTakenMessage, error.Message);
        Assert.AreEqual(3, deadlock.Failures);
        Assert.IsFalse(await host.Db.Appointments.AnyAsync());
        Assert.IsFalse(await host.Db.Notifications.AnyAsync());
    }

    /// <summary>Rescheduling takes the same path: a lost deadlock runs again and moves the appointment.</summary>
    [TestMethod]
    public async Task RescheduleAsync_LosesDeadlockOnce_RetriesAndMoves()
    {
        var deadlock = new SqlErrorInterceptor<Appointment>(1205);
        var (host, trainer, client) = await ArrangeAsync(deadlock);
        await using var _ = host;
        var appointment = await host.AddAppointmentAsync(client, trainer, Monday.AddHours(9));
        deadlock.FailuresLeft = 1;

        Assert.IsTrue(await host.Get<IAppointmentService>().RescheduleAsync(appointment.AppointmentId, trainer.UserId, Monday.AddHours(11), 60,
            await VersionAsync(host, appointment.AppointmentId)));

        Assert.AreEqual(1, deadlock.Failures);
        Assert.AreEqual(Monday.AddHours(11), (await host.Db.Appointments.AsNoTracking().SingleAsync()).StartDateTime);
    }
}
