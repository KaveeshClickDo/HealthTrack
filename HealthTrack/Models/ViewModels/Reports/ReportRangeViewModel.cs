using System.Globalization;

namespace HealthTrack.Models.ViewModels.Reports
{
    /// <summary>
    /// A report's date range (spec 1.7): the last 30 days unless the user picks dates.
    /// </summary>
    public class ReportRangeViewModel
    {
        /// <summary>The default range: the last 30 days, today included.</summary>
        public const int DefaultDays = 30;

        /// <summary>First day included.</summary>
        public DateTime From { get; init; }

        /// <summary>Last day included.</summary>
        public DateTime To { get; init; }

        /// <summary>The range as text, for example "26 Sep 2026 – 25 Oct 2026".</summary>
        public string Text =>
            $"{From.ToString("dd MMM yyyy", CultureInfo.CurrentCulture)} – {To.ToString("dd MMM yyyy", CultureInfo.CurrentCulture)}";

        /// <summary>
        /// Turns the query-string dates into a range: the last 30 days by default; dates in the wrong order are swapped. A
        /// range lies within the last ten years and ends today at the latest (reports describe what has happened), and it is
        /// at most two years long.
        /// </summary>
        /// <param name="from">The first day asked for.</param>
        /// <param name="to">The last day asked for.</param>
        /// <param name="today">Today's date.</param>
        /// <returns>The resolved range.</returns>
        public static ReportRangeViewModel Resolve(DateTime? from, DateTime? to, DateTime today)
        {
            // Clamping first also keeps a crafted 0001-01-01 or 9999-12-31 from overflowing the date arithmetic here and in
            // the reports (the day after the range, two years back).
            today = today.Date;
            var earliest = today.AddYears(-10);
            var last = Clamp((to ?? today).Date, earliest, today);
            var first = Clamp((from ?? last.AddDays(1 - DefaultDays)).Date, earliest, today);
            if (last < first)
            {
                (first, last) = (last, first);
            }

            if (first < last.AddYears(-2))
            {
                first = last.AddYears(-2);
            }

            return new ReportRangeViewModel { From = first, To = last };
        }

        private static DateTime Clamp(DateTime value, DateTime earliest, DateTime latest) =>
            value < earliest ? earliest : value > latest ? latest : value;
    }
}
