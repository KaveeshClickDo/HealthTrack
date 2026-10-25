using HealthTrack.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;

namespace HealthTrack.Tests.Security;

/// <summary>
/// The EF Core value converter behind the six encrypted columns (BR-20), with a real EncryptionService.
/// </summary>
[TestClass]
public sealed class EncryptedStringConverterTests
{
    private static EncryptedStringConverter NewConverter() =>
        new(new EncryptionService(new EphemeralDataProtectionProvider(), NullLogger<EncryptionService>.Instance));

    /// <summary>What goes to the database is ciphertext, and it reads back as the original text.</summary>
    [TestMethod]
    public void ConvertToProviderAndBack_MedicalText_StoresCiphertextAndRoundTrips()
    {
        var converter = NewConverter();

        var stored = (string?)converter.ConvertToProvider("Penicillin allergy");
        var read = (string?)converter.ConvertFromProvider(stored);

        Assert.IsNotNull(stored);
        Assert.AreNotEqual("Penicillin allergy", stored);
        Assert.DoesNotContain("Penicillin", stored);
        Assert.AreEqual("Penicillin allergy", read);
    }

    /// <summary>The same text encrypts differently each time, so equal values can't be spotted in the table.</summary>
    [TestMethod]
    public void ConvertToProvider_SameTextTwice_DifferentCiphertext()
    {
        var converter = NewConverter();

        Assert.AreNotEqual(converter.ConvertToProvider("Asthma"), converter.ConvertToProvider("Asthma"));
    }

    /// <summary>NULL stays NULL in both directions.</summary>
    [TestMethod]
    public void Convert_Null_StaysNull()
    {
        var converter = NewConverter();

        Assert.IsNull(converter.ConvertToProvider(null));
        Assert.IsNull(converter.ConvertFromProvider(null));
    }
}
