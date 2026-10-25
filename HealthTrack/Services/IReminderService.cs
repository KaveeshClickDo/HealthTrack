namespace HealthTrack.Services
{
    /// <summary>
    /// Creates the due workout, meal and appointment reminders, each exactly once and only as the user's settings allow
    /// (spec 1.8, BR-19, UC34).
    /// </summary>
    public interface IReminderService
    {
        /// <summary>
        /// Creates the reminders now due for one user: run by the bell's poll and the dashboards, so reminders appear
        /// without waiting for the background service.
        /// </summary>
        /// <param name="userId">The signed-in user.</param>
        /// <param name="now">The current time.</param>
        /// <returns>How many reminders were created.</returns>
        Task<int> GenerateDueRemindersAsync(string userId, DateTime now);

        /// <summary>
        /// Creates the reminders now due for every active user: run by <see cref="ReminderBackgroundService"/>.
        /// </summary>
        /// <param name="now">The current time.</param>
        /// <returns>How many reminders were created.</returns>
        Task<int> GenerateAllDueRemindersAsync(DateTime now);
    }
}
