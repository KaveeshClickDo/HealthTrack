namespace HealthTrack.Models.ViewModels.Reports
{
    /// <summary>
    /// A report exported as a file (UC40).
    /// </summary>
    /// <param name="FileName">The download name, for example client-performance-20261025.csv.</param>
    /// <param name="ContentType">The media type.</param>
    /// <param name="Content">The file's bytes.</param>
    public sealed record ReportFile(string FileName, string ContentType, byte[] Content);
}
