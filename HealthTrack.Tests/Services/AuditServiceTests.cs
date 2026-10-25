using HealthTrack.Models.Entities;
using HealthTrack.Models.Enums;
using HealthTrack.Models.ViewModels;
using HealthTrack.Security;
using HealthTrack.Services;
using HealthTrack.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Tests.Services;

/// <summary>
/// The audit trail: writing rows and the filtered, paged viewer (UC33).
/// </summary>
[TestClass]
public sealed class AuditServiceTests
{
    /// <summary>A row takes the clock's time, has no IP outside a real request, and long details are cut to the column size.</summary>
    [TestMethod]
    public async Task LogAsync_Event_SavesWithClockTimeAndTruncatedDetails()
    {
        await using var host = await TestHost.CreateAsync();

        await host.Get<IAuditService>().LogAsync(null, AuditAction.Export, "Report", "42", new string('x', 600));

        var row = await host.Db.AuditLogs.SingleAsync();
        Assert.AreEqual(host.Clock.Now, row.Timestamp);
        Assert.AreEqual(AuditAction.Export, row.Action);
        Assert.AreEqual("Report", row.EntityName);
        Assert.AreEqual("42", row.EntityId);
        Assert.AreEqual(500, row.Details!.Length);
        Assert.IsNull(row.IpAddress);
    }

    /// <summary>25 rows page as 20 + 5, newest first; a page past the end shows the last page.</summary>
    [TestMethod]
    public async Task GetPageAsync_TwentyFiveRows_PagesAtTwentyNewestFirst()
    {
        await using var host = await TestHost.CreateAsync();
        for (var i = 0; i < 25; i++)
        {
            host.Db.AuditLogs.Add(new AuditLog { Action = AuditAction.Login, Details = $"#{i}", Timestamp = host.Clock.Now.AddMinutes(i) });
        }

        await host.Db.SaveChangesAsync();
        var audit = host.Get<IAuditService>();

        var first = await audit.GetPageAsync(new AuditLogFilterViewModel(), 1);
        var last = await audit.GetPageAsync(new AuditLogFilterViewModel(), 99);

        Assert.HasCount(20, first.Items);
        Assert.AreEqual("#24", first.Items[0].Details);
        Assert.AreEqual(25, first.TotalCount);
        Assert.AreEqual(2, first.TotalPages);
        Assert.IsTrue(first.HasNext);
        Assert.AreEqual(2, last.Page);
        Assert.HasCount(5, last.Items);
        Assert.AreEqual("#0", last.Items[^1].Details);
    }

    /// <summary>Email (any case, part of it), action and an inclusive date range narrow the list.</summary>
    [TestMethod]
    public async Task GetPageAsync_Filters_NarrowByEmailActionAndDates()
    {
        await using var host = await TestHost.CreateAsync();
        var amaya = await host.CreateUserAsync("amaya@example.test", AppRoles.Client, firstName: "Amaya", lastName: "Perera");
        var ravi = await host.CreateUserAsync("ravi@example.test", AppRoles.Client);
        var today = host.Clock.Today;
        host.Db.AuditLogs.AddRange(
            new AuditLog { UserId = amaya.Id, Action = AuditAction.Login, Timestamp = today.AddDays(-3).AddHours(8) },
            new AuditLog { UserId = amaya.Id, Action = AuditAction.Login, Timestamp = today.AddDays(-1).AddHours(23) },
            new AuditLog { UserId = amaya.Id, Action = AuditAction.Logout, Timestamp = today.AddDays(-1).AddHours(23).AddMinutes(30) },
            new AuditLog { UserId = ravi.Id, Action = AuditAction.Login, Timestamp = today.AddDays(-1).AddHours(9) },
            new AuditLog { UserId = null, Action = AuditAction.LoginFailed, Timestamp = today.AddDays(-1).AddHours(10) });
        await host.Db.SaveChangesAsync();

        var page = await host.Get<IAuditService>().GetPageAsync(new AuditLogFilterViewModel
        {
            UserEmail = "AMAYA@",
            AuditAction = AuditAction.Login,
            From = today.AddDays(-2),
            To = today.AddDays(-1)
        }, 1);

        var row = page.Items.Single();
        Assert.AreEqual("amaya@example.test", row.UserEmail);
        Assert.AreEqual("Amaya Perera", row.UserName);
        Assert.AreEqual(today.AddDays(-1).AddHours(23), row.Timestamp);
    }

    /// <summary>A crafted "to" of 9999-12-31 has no day after it: the filter just has no end instead of failing.</summary>
    [TestMethod]
    public async Task GetPageAsync_LastPossibleToDate_NoError()
    {
        await using var host = await TestHost.CreateAsync();
        host.Db.AuditLogs.Add(new AuditLog { Action = AuditAction.Login, Timestamp = host.Clock.Now });
        await host.Db.SaveChangesAsync();

        var page = await host.Get<IAuditService>().GetPageAsync(new AuditLogFilterViewModel { To = DateTime.MaxValue }, 1);

        Assert.AreEqual(1, page.TotalCount);
    }
}
