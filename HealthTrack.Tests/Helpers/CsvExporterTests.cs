using System.Globalization;
using System.Text;
using HealthTrack.Helpers;
using HealthTrack.Models.Enums;

namespace HealthTrack.Tests.Helpers;

/// <summary>
/// CSV export (UC40): RFC 4180 quoting, the formula-injection guard and culture-independent numbers.
/// </summary>
[TestClass]
public sealed class CsvExporterTests
{
    /// <summary>A header row, then one line per row, every line ending in CRLF.</summary>
    [TestMethod]
    public void ToCsv_Rows_HeaderThenRowsWithCrlf()
    {
        var rows = new[] { ("Amaya", 3), ("Ravi", 5) };
        CsvColumn<(string Name, int Count)>[] columns = [new("Client", r => r.Name), new("Workouts", r => r.Count)];

        var csv = CsvExporter.ToCsv(rows, columns);

        Assert.AreEqual("Client,Workouts\r\nAmaya,3\r\nRavi,5\r\n", csv);
    }

    /// <summary>RFC 4180: a comma, a quote or a line break wraps the field in quotes, and inner quotes are doubled.</summary>
    [TestMethod]
    [DataRow("Perera, Amaya", "\"Perera, Amaya\"")]
    [DataRow("She said \"great\"", "\"She said \"\"great\"\"\"")]
    [DataRow("Line one\nLine two", "\"Line one\nLine two\"")]
    [DataRow("Plain text", "Plain text")]
    public void Field_SpecialCharacters_QuotedPerRfc4180(string value, string expected)
    {
        Assert.AreEqual(expected, CsvExporter.Field(value));
    }

    /// <summary>Text that a spreadsheet would run as a formula gets a leading apostrophe (CSV injection).</summary>
    [TestMethod]
    [DataRow("=HYPERLINK(\"http://evil.example\")", "\"'=HYPERLINK(\"\"http://evil.example\"\")\"")]
    [DataRow("+1+1", "'+1+1")]
    [DataRow("-2+3", "'-2+3")]
    [DataRow("@SUM(A1)", "'@SUM(A1)")]
    [DataRow("\tcmd", "'\tcmd")]
    public void Field_FormulaStart_GetsLeadingApostrophe(string value, string expected)
    {
        Assert.AreEqual(expected, CsvExporter.Field(value));
    }

    /// <summary>
    /// Numbers stay numbers (a negative weight change isn't a formula), with a dot and no thousands separator whatever the
    /// culture; dates are ISO, booleans Yes/No, enums their display name and null an empty field.
    /// </summary>
    [TestMethod]
    public void Field_NonText_FormattedWithoutGuardOrCulture()
    {
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

            Assert.AreEqual("-1.5", CsvExporter.Field(-1.5m));
            Assert.AreEqual("1234.5", CsvExporter.Field(1234.5m));
            Assert.AreEqual("-3", CsvExporter.Field(-3));
            Assert.AreEqual("2026-10-25", CsvExporter.Field(new DateTime(2026, 10, 25, 14, 30, 0)));
            Assert.AreEqual("Yes", CsvExporter.Field(true));
            Assert.AreEqual("Fitness trainer", CsvExporter.Field(ProfessionalType.FitnessTrainer));
            Assert.AreEqual(string.Empty, CsvExporter.Field(null));
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    /// <summary>The file starts with a UTF-8 byte-order mark, so Excel reads accented names correctly.</summary>
    [TestMethod]
    public void ToBytes_Text_Utf8WithByteOrderMark()
    {
        var bytes = CsvExporter.ToBytes("Zoë\r\n");

        CollectionAssert.AreEqual(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3).ToArray());
        Assert.AreEqual("Zoë\r\n", Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3));
    }
}
