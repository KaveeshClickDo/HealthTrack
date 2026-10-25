using System.Globalization;
using HealthTrack.Helpers;
using HealthTrack.Models.ViewModels;
using HealthTrack.Services;
using Microsoft.AspNetCore.Mvc;

namespace HealthTrack.Controllers
{
    /// <summary>
    /// The signed-in user's notifications and reminder settings (UC06), and the JSON poll behind the bell. Open to every role.
    /// </summary>
    public class NotificationsController : BaseController
    {
        // The bell shows the newest few unread notifications.
        private const int PollItems = 5;

        private readonly INotificationService _notifications;
        private readonly IReminderService _reminders;
        private readonly IClock _clock;

        /// <summary>
        /// Creates the controller.
        /// </summary>
        /// <param name="notifications">The notification service.</param>
        /// <param name="reminders">Creates the user's due reminders before a poll.</param>
        /// <param name="clock">Supplies the time for the reminder check.</param>
        public NotificationsController(INotificationService notifications, IReminderService reminders, IClock clock)
        {
            _notifications = notifications;
            _reminders = reminders;
            _clock = clock;
        }

        /// <summary>
        /// All the user's notifications, newest first, 20 per page; unread ones are highlighted.
        /// </summary>
        /// <param name="page">The page.</param>
        /// <returns>The list.</returns>
        [HttpGet]
        public async Task<IActionResult> Index(int page = 1) => View(await _notifications.GetPageAsync(CurrentUserId, page));

        /// <summary>
        /// For wwwroot/js/healthtrack.js every 60 seconds: creates the user's due reminders (spec 1.8), then returns the
        /// unread count and the newest unread items.
        /// </summary>
        /// <returns>{ count, items: [{ id, title, message, linkUrl, createdAt }] }.</returns>
        [HttpGet]
        public async Task<IActionResult> Poll()
        {
            await _reminders.GenerateDueRemindersAsync(CurrentUserId, _clock.Now);
            var count = await _notifications.GetUnreadCountAsync(CurrentUserId);
            var items = await _notifications.GetUnreadAsync(CurrentUserId, PollItems);

            return Json(new
            {
                count,
                items = items.Select(n => new
                {
                    id = n.NotificationId,
                    title = n.Title,
                    message = n.Message,
                    linkUrl = n.LinkUrl,
                    createdAt = n.CreatedAt.ToString("dd MMM yyyy HH:mm", CultureInfo.CurrentCulture)
                })
            });
        }

        /// <summary>
        /// Marks one of the user's notifications as read: from the list (a form) or from a toast (AJAX).
        /// </summary>
        /// <param name="id">The notification.</param>
        /// <param name="page">The list page to return to.</param>
        /// <returns>204 for AJAX, otherwise the list; 404 for someone else's notification.</returns>
        [HttpPost]
        public async Task<IActionResult> MarkAsRead(int id, int page = 1)
        {
            if (!await _notifications.MarkAsReadAsync(CurrentUserId, id))
            {
                return NotFound();
            }

            return IsAjaxRequest() ? NoContent() : RedirectToAction(nameof(Index), new { page });
        }

        /// <summary>
        /// Marks all the user's notifications as read.
        /// </summary>
        /// <returns>The list.</returns>
        [HttpPost]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var count = await _notifications.MarkAllAsReadAsync(CurrentUserId);
            SetSuccess(count == 0 ? "You had no unread notifications." : $"{count} notification{(count == 1 ? string.Empty : "s")} marked as read.");
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// The reminder settings form.
        /// </summary>
        /// <returns>The form.</returns>
        [HttpGet]
        public async Task<IActionResult> Preferences()
        {
            var model = await _notifications.GetPreferencesAsync(CurrentUserId);
            return model is null ? NotFound() : View(model);
        }

        /// <summary>
        /// Saves the reminder settings.
        /// </summary>
        /// <param name="model">The form.</param>
        /// <returns>The form again with a success message, or with its errors.</returns>
        [HttpPost]
        public async Task<IActionResult> Preferences(ReminderPreferencesViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    if (!await _notifications.SavePreferencesAsync(CurrentUserId, model))
                    {
                        return NotFound();
                    }

                    SetSuccess("Your notification settings have been saved.");
                    return RedirectToAction(nameof(Preferences));
                }
                catch (BusinessRuleException ex)
                {
                    ModelState.AddModelError(string.Empty, ex.Message);
                }
            }

            await _notifications.FillPreferencesDisplayAsync(model, CurrentUserId);
            return View(model);
        }

        // healthtrack.js sends this header, as jQuery does.
        private bool IsAjaxRequest() =>
            string.Equals(Request.Headers.XRequestedWith, "XMLHttpRequest", StringComparison.Ordinal);
    }
}
