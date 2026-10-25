namespace HealthTrack.Security
{
    /// <summary>
    /// Encrypts and decrypts the sensitive free-text health fields stored in the database (BR-20).
    /// </summary>
    public interface IEncryptionService
    {
        /// <summary>
        /// Encrypts a value for storage.
        /// </summary>
        /// <param name="plainText">The value to protect; null stays null.</param>
        /// <returns>Base64 ciphertext, or null.</returns>
        string? Encrypt(string? plainText);

        /// <summary>
        /// Decrypts a stored value.
        /// </summary>
        /// <param name="cipherText">The stored ciphertext; null stays null.</param>
        /// <returns>The plain text, or null when the value is null or cannot be decrypted.</returns>
        string? Decrypt(string? cipherText);
    }
}
