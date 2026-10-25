using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace HealthTrack.Security
{
    /// <summary>
    /// Encrypts health fields with ASP.NET Core Data Protection (AES-256-CBC with HMAC-SHA256; keys in App_Data/keys).
    /// Registered as a singleton: EF Core caches the model, so the value converter keeps the first instance it is given.
    /// </summary>
    public class EncryptionService : IEncryptionService
    {
        /// <summary>The Data Protection purpose string; changing it makes existing ciphertext unreadable.</summary>
        public const string Purpose = "HealthTrack.HealthData.v1";

        private readonly IDataProtector _protector;
        private readonly ILogger<EncryptionService> _logger;

        /// <summary>
        /// Creates the service with a protector for <see cref="Purpose"/>.
        /// </summary>
        /// <param name="provider">The app's Data Protection provider.</param>
        /// <param name="logger">Logs values that cannot be decrypted (never the values themselves).</param>
        public EncryptionService(IDataProtectionProvider provider, ILogger<EncryptionService> logger)
        {
            _protector = provider.CreateProtector(Purpose);
            _logger = logger;
        }

        /// <inheritdoc />
        public string? Encrypt(string? plainText) => plainText is null ? null : _protector.Protect(plainText);

        /// <inheritdoc />
        public string? Decrypt(string? cipherText)
        {
            if (cipherText is null)
            {
                return null;
            }

            try
            {
                return _protector.Unprotect(cipherText);
            }
            catch (CryptographicException ex)
            {
                // A lost key or a damaged value shouldn't crash the page; the field shows as empty instead.
                _logger.LogWarning(ex, "A protected health value could not be decrypted and is shown as empty.");
                return null;
            }
        }
    }
}
