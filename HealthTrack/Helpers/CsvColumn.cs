namespace HealthTrack.Helpers
{
    /// <summary>
    /// One column of a CSV export: its header and how to read the value from a row.
    /// </summary>
    /// <typeparam name="T">The row type.</typeparam>
    /// <param name="Header">The column header.</param>
    /// <param name="Value">Reads the cell value; numbers, dates, enums and booleans are formatted by <see cref="CsvExporter"/>.</param>
    public sealed record CsvColumn<T>(string Header, Func<T, object?> Value);
}
