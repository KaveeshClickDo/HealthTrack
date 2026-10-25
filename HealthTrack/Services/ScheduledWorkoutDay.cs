namespace HealthTrack.Services
{
    /// <summary>
    /// A day on which a client's plan in effect scheduled training (see <see cref="WorkoutAdherence"/>).
    /// </summary>
    /// <param name="ClientId">The client.</param>
    /// <param name="WorkoutPlanId">The plan in effect that day.</param>
    /// <param name="Date">The day.</param>
    /// <param name="Logged">Whether a session was logged that day.</param>
    internal sealed record ScheduledWorkoutDay(int ClientId, int WorkoutPlanId, DateTime Date, bool Logged);
}
