using HealthTrack.Helpers;
using Microsoft.Extensions.Options;

namespace HealthTrack.Services
{
    /// <summary>
    /// The Timer actor of UC34: every Reminders:IntervalSeconds (60 by default) it creates the reminders due for every
    /// user, so reminders and their email copies go out even when nobody is signed in (spec 1.8).
    /// </summary>
    public class ReminderBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly ReminderOptions _options;
        private readonly IClock _clock;
        private readonly ILogger<ReminderBackgroundService> _logger;

        /// <summary>
        /// Creates the service.
        /// </summary>
        /// <param name="scopes">Creates a DI scope (and so a fresh DbContext) for each run.</param>
        /// <param name="options">The "Reminders" settings.</param>
        /// <param name="clock">Supplies the time.</param>
        /// <param name="logger">Records each run's outcome.</param>
        public ReminderBackgroundService(IServiceScopeFactory scopes, IOptions<ReminderOptions> options, IClock clock,
            ILogger<ReminderBackgroundService> logger)
        {
            _scopes = scopes;
            _options = options.Value;
            _clock = clock;
            _logger = logger;
        }

        /// <summary>
        /// Runs until the app stops; does nothing at all when Reminders:Enabled is false.
        /// </summary>
        /// <param name="stoppingToken">Signals that the app is stopping.</param>
        /// <returns>A task that completes when the app stops.</returns>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.Enabled)
            {
                _logger.LogInformation("Background reminders are switched off (Reminders:Enabled is false).");
                return;
            }

            // At least 10 seconds, so a typo in the settings can't make the loop hammer the database.
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(10, _options.IntervalSeconds)));
            try
            {
                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    await RunOnceAsync();
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // The app is stopping.
            }
        }

        private async Task RunOnceAsync()
        {
            try
            {
                // A new scope each tick: a DbContext is meant for one unit of work, not for the app's lifetime.
                await using var scope = _scopes.CreateAsyncScope();
                var reminders = scope.ServiceProvider.GetRequiredService<IReminderService>();
                var created = await reminders.GenerateAllDueRemindersAsync(_clock.Now);
                if (created > 0)
                {
                    _logger.LogInformation("Created {Count} reminder(s).", created);
                }
            }
            catch (Exception ex)
            {
                // Anything that goes wrong in one run (the database briefly unavailable, say) is logged, and the next tick
                // tries again; an exception escaping ExecuteAsync would stop the whole app.
                _logger.LogError(ex, "The reminder run failed; it will run again at the next tick.");
            }
        }
    }
}
