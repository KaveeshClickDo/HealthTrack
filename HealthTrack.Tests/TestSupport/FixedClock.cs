using HealthTrack.Helpers;

namespace HealthTrack.Tests.TestSupport;

/// <summary>
/// A clock that shows whatever time the test sets.
/// </summary>
public sealed class FixedClock : IClock
{
    /// <summary>
    /// Creates the clock.
    /// </summary>
    /// <param name="now">The time to show.</param>
    public FixedClock(DateTime now)
    {
        Now = now;
    }

    /// <summary>The time shown; a test may move it.</summary>
    public DateTime Now { get; set; }

    /// <inheritdoc />
    public DateTime Today => Now.Date;
}
