using HealthTrack.Models.Entities;
using HealthTrack.Models.Enums;
using HealthTrack.Models.ViewModels;
using HealthTrack.Services;
using HealthTrack.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Tests.Services;

/// <summary>
/// Trainer feedback (UC15, UC22): assigned clients only, sessions must be the client's own (BR-08).
/// </summary>
[TestClass]
public sealed class FeedbackServiceTests
{
    private static async Task<(TestHost Host, TrainerProfile Trainer, ClientProfile Client)> ArrangeAsync()
    {
        var host = await TestHost.CreateAsync();
        var trainer = await host.CreateTrainerAsync("trainer@example.test", firstName: "Chris");
        var client = await host.CreateClientAsync("client@example.test");
        await host.AssignAsync(trainer, client);
        return (host, trainer, client);
    }

    private static async Task<WorkoutLog> AddLogAsync(TestHost host, ClientProfile client)
    {
        var log = new WorkoutLog { ClientId = client.ClientId, WorkoutDate = host.Clock.Today, CreatedAt = host.Clock.Now };
        host.Db.WorkoutLogs.Add(log);
        await host.Db.SaveChangesAsync();
        return log;
    }

    /// <summary>Feedback on an assigned client's session is saved and the client is notified.</summary>
    [TestMethod]
    public async Task GiveFeedbackAsync_AssignedClientsSession_SavesAndNotifies()
    {
        var (host, trainer, client) = await ArrangeAsync();
        await using var _ = host;
        var log = await AddLogAsync(host, client);

        var feedbackId = await host.Get<IFeedbackService>().GiveFeedbackAsync(trainer.UserId, new FeedbackViewModel
        {
            ClientId = client.ClientId,
            WorkoutLogId = log.WorkoutLogId,
            Category = FeedbackCategory.Workout,
            Message = "  Great consistency this week. ",
            Rating = 4
        });

        var saved = await host.Db.Feedback.AsNoTracking().SingleAsync();
        Assert.AreEqual(saved.FeedbackId, feedbackId);
        Assert.AreEqual("Great consistency this week.", saved.Message);
        Assert.AreEqual(trainer.TrainerId, saved.TrainerId);
        Assert.AreEqual(log.WorkoutLogId, saved.WorkoutLogId);
        var notification = await host.Db.Notifications.SingleAsync();
        Assert.AreEqual(client.UserId, notification.UserId);
        Assert.AreEqual(NotificationType.NewFeedback, notification.Type);
        Assert.AreEqual("New feedback from Chris User", notification.Title);
    }

    /// <summary>BR-08: no feedback for a client who isn't assigned to the trainer.</summary>
    [TestMethod]
    public async Task GiveFeedbackAsync_UnassignedClient_ReturnsNull()
    {
        var (host, _, client) = await ArrangeAsync();
        await using var __ = host;
        var stranger = await host.CreateTrainerAsync("stranger@example.test");

        Assert.IsNull(await host.Get<IFeedbackService>().GiveFeedbackAsync(stranger.UserId, new FeedbackViewModel
        {
            ClientId = client.ClientId,
            Category = FeedbackCategory.General,
            Message = "Hello"
        }));
        Assert.IsFalse(await host.Db.Feedback.AnyAsync());
    }

    /// <summary>The session must belong to the client the feedback is for.</summary>
    [TestMethod]
    public async Task GiveFeedbackAsync_OtherClientsSession_ReturnsNull()
    {
        var (host, trainer, client) = await ArrangeAsync();
        await using var _ = host;
        var other = await host.CreateClientAsync("other@example.test");
        await host.AssignAsync(trainer, other);
        var othersLog = await AddLogAsync(host, other);
        var feedback = host.Get<IFeedbackService>();

        Assert.IsNull(await feedback.GiveFeedbackAsync(trainer.UserId, new FeedbackViewModel
        {
            ClientId = client.ClientId,
            WorkoutLogId = othersLog.WorkoutLogId,
            Category = FeedbackCategory.Workout,
            Message = "Hi"
        }));
        Assert.IsNull(await feedback.GetFeedbackFormAsync(trainer.UserId, client.ClientId, othersLog.WorkoutLogId));
    }

    /// <summary>The form for a chosen client lists their sessions to pick from.</summary>
    [TestMethod]
    public async Task GetFeedbackFormAsync_AssignedClient_ListsTheirSessions()
    {
        var (host, trainer, client) = await ArrangeAsync();
        await using var _ = host;
        var log = await AddLogAsync(host, client);

        var form = await host.Get<IFeedbackService>().GetFeedbackFormAsync(trainer.UserId, client.ClientId, null);

        Assert.IsNotNull(form);
        Assert.AreEqual("Client User", form.ClientName);
        Assert.AreEqual(log.WorkoutLogId, form.SessionOptions.Single().Id);
        Assert.HasCount(1, form.ClientOptions);
    }

    /// <summary>UC15: the client reads their feedback newest first.</summary>
    [TestMethod]
    public async Task GetForClientAsync_SeveralItems_NewestFirst()
    {
        var (host, trainer, client) = await ArrangeAsync();
        await using var _ = host;
        host.Db.Feedback.AddRange(
            new Feedback { TrainerId = trainer.TrainerId, ClientId = client.ClientId, Category = FeedbackCategory.Diet, Message = "Older", CreatedAt = host.Clock.Now.AddDays(-2) },
            new Feedback { TrainerId = trainer.TrainerId, ClientId = client.ClientId, Category = FeedbackCategory.Progress, Message = "Newer", CreatedAt = host.Clock.Now });
        await host.Db.SaveChangesAsync();

        var page = await host.Get<IFeedbackService>().GetForClientAsync(client.ClientId, 1);

        CollectionAssert.AreEqual(new[] { "Newer", "Older" }, page.Items.Select(f => f.Message).ToArray());
        Assert.AreEqual("Chris User", page.Items[0].TrainerName);
    }
}
