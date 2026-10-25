using System.Globalization;

namespace HealthTrack.Helpers
{
    /// <summary>
    /// How a prescription reads on screen, so the trainer's editor, the client's week and today's log all say it the same way.
    /// </summary>
    public static class WorkoutText
    {
        /// <summary>The weekdays in the order the app shows them: Monday first.</summary>
        public static readonly IReadOnlyList<DayOfWeek> WeekFromMonday =
        [
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
            DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
        ];

        /// <summary>
        /// A prescription as text, for example "3 × 10 · rest 60 s" or "30 min".
        /// </summary>
        /// <param name="sets">Sets, if prescribed.</param>
        /// <param name="reps">Reps per set, if prescribed.</param>
        /// <param name="durationMinutes">Duration, if prescribed.</param>
        /// <param name="restSeconds">Rest between sets, if prescribed.</param>
        /// <returns>The text; empty when nothing is prescribed.</returns>
        public static string Prescription(int? sets, int? reps, int? durationMinutes, int? restSeconds = null)
        {
            var parts = new List<string>();
            if (sets is > 0 && reps is > 0)
            {
                parts.Add(string.Create(CultureInfo.InvariantCulture, $"{sets} × {reps}"));
            }

            if (durationMinutes is > 0)
            {
                parts.Add(string.Create(CultureInfo.InvariantCulture, $"{durationMinutes} min"));
            }

            if (restSeconds is > 0)
            {
                parts.Add(string.Create(CultureInfo.InvariantCulture, $"rest {restSeconds} s"));
            }

            return string.Join(" · ", parts);
        }
    }
}
