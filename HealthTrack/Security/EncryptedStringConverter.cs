using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace HealthTrack.Security
{
    /// <summary>
    /// EF Core value converter that stores a string column as ciphertext and reads it back as plain text (BR-20).
    /// Nulls are stored as NULL without calling the encryption service.
    /// </summary>
    public class EncryptedStringConverter : ValueConverter<string?, string?>
    {
        /// <summary>
        /// Creates the converter around an encryption service.
        /// </summary>
        /// <param name="encryption">The service that encrypts on save and decrypts on read.</param>
        public EncryptedStringConverter(IEncryptionService encryption)
            : base(plainText => encryption.Encrypt(plainText), cipherText => encryption.Decrypt(cipherText))
        {
        }
    }
}
