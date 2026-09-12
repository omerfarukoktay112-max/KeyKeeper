using System.Security.Cryptography;
using System.Text;
using KeyKeeper.Core.Crypto;

namespace KeyKeeper.Core.Tests.Crypto;

public class AesGcmEncryptionServiceTests
{
    private readonly AesGcmEncryptionService _sut = new();

    private static byte[] NewKey() => RandomNumberGenerator.GetBytes(32);

    [Fact]
    public void EncryptThenDecrypt_WithCorrectKey_RoundTripsPlaintext()
    {
        byte[] key = NewKey();
        byte[] plaintext = Encoding.UTF8.GetBytes("correct horse battery staple vault secret");

        byte[] packed = _sut.Encrypt(plaintext, key);
        byte[] decrypted = _sut.Decrypt(packed, key);

        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void EncryptThenDecrypt_WithEmptyPlaintext_RoundTrips()
    {
        byte[] key = NewKey();
        byte[] plaintext = Array.Empty<byte>();

        byte[] packed = _sut.Encrypt(plaintext, key);
        byte[] decrypted = _sut.Decrypt(packed, key);

        Assert.Empty(decrypted);
    }

    [Fact]
    public void Decrypt_WithWrongKey_ThrowsVaultCryptographyException()
    {
        byte[] correctKey = NewKey();
        byte[] wrongKey = NewKey();
        byte[] packed = _sut.Encrypt(Encoding.UTF8.GetBytes("secret data"), correctKey);

        Assert.Throws<VaultCryptographyException>(() => _sut.Decrypt(packed, wrongKey));
    }

    [Fact]
    public void Decrypt_WithTamperedCiphertext_ThrowsVaultCryptographyException()
    {
        byte[] key = NewKey();
        byte[] packed = _sut.Encrypt(Encoding.UTF8.GetBytes("secret data"), key);

        // Flip a bit inside the ciphertext region (after the 12-byte nonce).
        packed[AesGcmEncryptionService.NonceSizeBytes] ^= 0xFF;

        Assert.Throws<VaultCryptographyException>(() => _sut.Decrypt(packed, key));
    }

    [Fact]
    public void Decrypt_WithTamperedTag_ThrowsVaultCryptographyException()
    {
        byte[] key = NewKey();
        byte[] packed = _sut.Encrypt(Encoding.UTF8.GetBytes("secret data"), key);

        // Flip the last byte, which lives inside the trailing authentication tag.
        packed[^1] ^= 0xFF;

        Assert.Throws<VaultCryptographyException>(() => _sut.Decrypt(packed, key));
    }

    [Fact]
    public void Decrypt_TooShortToContainNonceAndTag_ThrowsArgumentException()
    {
        byte[] key = NewKey();
        byte[] tooShort = new byte[10];

        Assert.Throws<ArgumentException>(() => _sut.Decrypt(tooShort, key));
    }

    [Fact]
    public void Encrypt_SamePlaintextTwice_ProducesDifferentNoncesAndCiphertexts()
    {
        byte[] key = NewKey();
        byte[] plaintext = Encoding.UTF8.GetBytes("same plaintext every time");

        byte[] packedA = _sut.Encrypt(plaintext, key);
        byte[] packedB = _sut.Encrypt(plaintext, key);

        byte[] nonceA = packedA[..AesGcmEncryptionService.NonceSizeBytes];
        byte[] nonceB = packedB[..AesGcmEncryptionService.NonceSizeBytes];

        Assert.NotEqual(nonceA, nonceB);
        Assert.NotEqual(packedA, packedB);

        // But both still decrypt back to the same original plaintext.
        Assert.Equal(plaintext, _sut.Decrypt(packedA, key));
        Assert.Equal(plaintext, _sut.Decrypt(packedB, key));
    }

    [Fact]
    public void Encrypt_PackedOutputLength_IsNonceLengthPlusPlaintextLengthPlusTagLength()
    {
        byte[] key = NewKey();
        byte[] plaintext = Encoding.UTF8.GetBytes("twelve bytes"); // arbitrary length

        byte[] packed = _sut.Encrypt(plaintext, key);

        Assert.Equal(
            AesGcmEncryptionService.NonceSizeBytes + plaintext.Length + AesGcmEncryptionService.TagSizeBytes,
            packed.Length);
    }
}
