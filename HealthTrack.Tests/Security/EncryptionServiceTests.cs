using HealthTrack.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;

namespace HealthTrack.Tests.Security;

/// <summary>
/// Encryption of the sensitive health fields (BR-20), using an in-memory Data Protection key ring.
/// </summary>
[TestClass]
public sealed class EncryptionServiceTests
{
    private static EncryptionService NewService(IDataProtectionProvider? provider = null) =>
        new(provider ?? new EphemeralDataProtectionProvider(), NullLogger<EncryptionService>.Instance);

    /// <summary>Null stays null in both directions.</summary>
    [TestMethod]
    public void EncryptAndDecrypt_Null_ReturnNull()
    {
        var service = NewService();

        Assert.IsNull(service.Encrypt(null));
        Assert.IsNull(service.Decrypt(null));
    }

    /// <summary>The stored value is ciphertext, and it decrypts back to the original.</summary>
    [TestMethod]
    public void Encrypt_PlainText_StoresCiphertextThatDecryptsBack()
    {
        var service = NewService();

        var cipherText = service.Encrypt("Type 2 diabetes");

        Assert.IsNotNull(cipherText);
        Assert.DoesNotContain("diabetes", cipherText);
        Assert.AreEqual("Type 2 diabetes", service.Decrypt(cipherText));
    }

    /// <summary>A damaged value doesn't crash the page; it reads as null.</summary>
    [TestMethod]
    public void Decrypt_TamperedCiphertext_ReturnsNull()
    {
        var service = NewService();
        var cipherText = service.Encrypt("Penicillin allergy")!;

        var tampered = cipherText[..^4] + (cipherText.EndsWith("AAAA", StringComparison.Ordinal) ? "BBBB" : "AAAA");

        Assert.IsNull(service.Decrypt(tampered));
    }

    /// <summary>Ciphertext from another key ring (a lost key) reads as null.</summary>
    [TestMethod]
    public void Decrypt_DifferentKeyRing_ReturnsNull()
    {
        var cipherText = NewService().Encrypt("Asthma");

        Assert.IsNull(NewService().Decrypt(cipherText));
    }
}
