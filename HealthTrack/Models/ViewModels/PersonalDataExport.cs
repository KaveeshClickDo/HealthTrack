namespace HealthTrack.Models.ViewModels
{
    /// <summary>
    /// A client's personal data as a JSON download (UC16, BR-22).
    /// </summary>
    /// <param name="FileName">The download name, for example healthtrack-my-data-20261025.json.</param>
    /// <param name="Content">The JSON, UTF-8.</param>
    public sealed record PersonalDataExport(string FileName, byte[] Content);
}
