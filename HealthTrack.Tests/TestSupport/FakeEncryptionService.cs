using HealthTrack.Security;

namespace HealthTrack.Tests.TestSupport;

/// <summary>
/// A predictable stand-in for <see cref="IEncryptionService"/>: "secret" is stored as "enc:secret". Stateless, so it is safe
/// even though EF Core caches the model (and with it the converter) for the whole test run.
/// </summary>
public sealed class FakeEncryptionService : IEncryptionService
{
    /// <summary>The prefix that marks a value as "encrypted".</summary>
    public const string Prefix = "enc:";

    /// <inheritdoc />
    public string? Encrypt(string? plainText) => plainText is null ? null : Prefix + plainText;

    /// <inheritdoc />
    public string? Decrypt(string? cipherText) =>
        cipherText is not null && cipherText.StartsWith(Prefix, StringComparison.Ordinal) ? cipherText[Prefix.Length..] : null;
}
