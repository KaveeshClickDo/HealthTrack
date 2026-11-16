namespace HealthTrack.Helpers
{
    /// <summary>
    /// Short, friendly day labels for lists ("Today", "Yesterday", "Fri 23 Oct").
    /// </summary>
    public static class DateLabels
    {
        /// <summary>
        /// Names a date relative to today: today, yesterday or tomorrow by name, otherwise the weekday and date.
        /// </summary>
        /// <param name="value">The date (any time of day).</param>
        /// <param name="today">Today's date, from <see cref="IClock"/>.</param>
        /// <returns>The label.</returns>
        public static string Day(DateTime value, DateTime today)
        {
            var days = (value.Date - today.Date).Days;
            return days switch
            {
                0 => "Today",
                -1 => "Yesterday",
                1 => "Tomorrow",
                _ when value.Year == today.Year => value.ToString("ddd d MMM"),
                _ => value.ToString("d MMM yyyy")
            };
        }

        /// <summary>
        /// A day label followed by the time, for example "Today · 09:30".
        /// </summary>
        /// <param name="value">The date and time.</param>
        /// <param name="today">Today's date, from <see cref="IClock"/>.</param>
        /// <returns>The label.</returns>
        public static string DayAndTime(DateTime value, DateTime today) => $"{Day(value, today)} · {value:HH:mm}";
    }
}
