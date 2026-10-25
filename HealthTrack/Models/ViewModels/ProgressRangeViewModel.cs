namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// The date range of the progress charts: the last 4 weeks, the last 3 months, or custom dates (spec 1.7).
    /// </summary>
    public class ProgressRangeViewModel
    {
        /// <summary>The last 4 weeks, today included.</summary>
        public const string FourWeeks = "4w";

        /// <summary>The last 3 months, today included.</summary>
        public const string ThreeMonths = "3m";

        /// <summary>Dates the user chose.</summary>
        public const string Custom = "custom";

        /// <summary>Which range: 4w, 3m or custom.</summary>
        public string Range { get; init; } = FourWeeks;

        /// <summary>First day included.</summary>
        public DateTime From { get; init; }

        /// <summary>Last day included.</summary>
        public DateTime To { get; init; }

        /// <summary>
        /// Turns the query-string values into a range. Unknown values give the last 4 weeks; custom dates are put in
        /// order, lie within the last ten years, end today at the latest and span at most two years.
        /// </summary>
        /// <param name="range">4w, 3m or custom.</param>
        /// <param name="from">The custom start.</param>
        /// <param name="to">The custom end.</param>
        /// <param name="today">Today's date.</param>
        /// <returns>The resolved range.</returns>
        public static ProgressRangeViewModel Resolve(string? range, DateTime? from, DateTime? to, DateTime today)
        {
            today = today.Date;

            if (range == ThreeMonths)
            {
                return new ProgressRangeViewModel { Range = ThreeMonths, From = today.AddMonths(-3).AddDays(1), To = today };
            }

            if (range == Custom && from is { } start && to is { } end)
            {
                // Clamping first also keeps a crafted 0001-01-01 from overflowing the "two years back" arithmetic below.
                var earliest = today.AddYears(-10);
                var first = Clamp(start.Date, earliest, today);
                var last = Clamp(end.Date, earliest, today);
                if (last < first)
                {
                    (first, last) = (last, first);
                }

                if (first < last.AddYears(-2))
                {
                    first = last.AddYears(-2);
                }

                return new ProgressRangeViewModel { Range = Custom, From = first, To = last };
            }

            return new ProgressRangeViewModel { Range = FourWeeks, From = today.AddDays(-27), To = today };
        }

        private static DateTime Clamp(DateTime value, DateTime earliest, DateTime latest) =>
            value < earliest ? earliest : value > latest ? latest : value;
    }
}
