using System.Globalization;
using System.Text;

namespace HealthTrack.Helpers
{
    /// <summary>
    /// Turns report rows into CSV (RFC 4180) for UC40: a header row, CRLF line endings, and fields with a comma, quote or
    /// line break wrapped in double quotes with inner quotes doubled.
    /// </summary>
    public static class CsvExporter
    {
        // Spreadsheets run a cell starting with these as a formula (CSV injection), so such text gets a leading apostrophe.
        // Tab and carriage return are on the OWASP list too.
        private static readonly char[] FormulaStarts = ['=', '+', '-', '@', '\t', '\r'];

        private static readonly char[] NeedsQuotes = [',', '"', '\r', '\n'];

        /// <summary>
        /// Writes the rows as CSV text.
        /// </summary>
        /// <typeparam name="T">The row type.</typeparam>
        /// <param name="rows">The rows, in order.</param>
        /// <param name="columns">The columns, in order.</param>
        /// <returns>The CSV, every line ending in CRLF.</returns>
        public static string ToCsv<T>(IEnumerable<T> rows, IReadOnlyList<CsvColumn<T>> columns)
        {
            var csv = new StringBuilder();
            AppendLine(csv, columns.Select(c => Field(c.Header)));
            foreach (var row in rows)
            {
                AppendLine(csv, columns.Select(c => Field(c.Value(row))));
            }

            return csv.ToString();
        }

        /// <summary>
        /// Encodes CSV text as UTF-8 with a byte-order mark, so Excel reads names with accents correctly.
        /// </summary>
        /// <param name="csv">The CSV text.</param>
        /// <returns>The file's bytes.</returns>
        public static byte[] ToBytes(string csv)
        {
            var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
            return [.. encoding.GetPreamble(), .. encoding.GetBytes(csv)];
        }

        /// <summary>
        /// Formats one cell. Numbers use a dot and no thousands separator, dates are yyyy-MM-dd, enums use their display
        /// name and booleans are Yes/No; text that starts like a formula gets a leading apostrophe. Numbers are never
        /// guarded: a negative number such as -1.2 is data, and Excel reads it as a number, not a formula.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The CSV field, quoted when needed.</returns>
        public static string Field(object? value)
        {
            var text = value switch
            {
                null => string.Empty,
                string s => Guard(s),
                DateTime date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                bool flag => flag ? "Yes" : "No",
                Enum item => Guard(item.GetDisplayName()),
                IFormattable number => number.ToString(null, CultureInfo.InvariantCulture),
                _ => Guard(value.ToString() ?? string.Empty)
            };

            return text.IndexOfAny(NeedsQuotes) >= 0 ? "\"" + text.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"" : text;
        }

        private static string Guard(string text) =>
            text.Length > 0 && FormulaStarts.Contains(text[0]) ? "'" + text : text;

        private static void AppendLine(StringBuilder csv, IEnumerable<string> fields) =>
            csv.Append(string.Join(',', fields)).Append("\r\n");
    }
}
