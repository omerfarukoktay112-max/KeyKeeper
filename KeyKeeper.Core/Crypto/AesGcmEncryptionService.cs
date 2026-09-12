using System.Security.Cryptography;

namespace KeyKeeper.Core.Crypto;

/// <summary>
/// AES-256-GCM <see cref="IEncryptionService"/> implementation.
///
/// Packed output layout: <c>nonce (12 bytes) || ciphertext (plaintext.Length bytes) || tag (16 bytes)</c>.
/// A fresh random nonce is generated for every <see cref="Encrypt"/> call — nonces are never reused.
/// </summary>
public sealed class AesGcmEncryptionService : IEncryptionService
{
    public const int NonceSizeBytes = 12;
    public const int TagSizeBytes = 16;

    public byte[] Encrypt(byte[] plaintext, byte[] key)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        ArgumentNullException.ThrowIfNull(key);

        byte[] nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[TagSizeBytes];

        using (var aesGcm = new AesGcm(key, TagSizeBytes))
        {
            aesGcm.Encrypt(nonce, plaintext, ciphertext, tag);
        }

        byte[] packed = new byte[NonceSizeBytes + ciphertext.Length + TagSizeBytes];
        Buffer.BlockCopy(nonce, 0, packed, 0, NonceSizeBytes);
        Buffer.BlockCopy(ciphertext, 0, packed, NonceSizeBytes, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, packed, NonceSizeBytes + ciphertext.Length, TagSizeBytes);

        return packed;
    }

    public byte[] Decrypt(byte[] packedCiphertext, byte[] key)
    {
        ArgumentNullException.ThrowIfNull(packedCiphertext);
        ArgumentNullException.ThrowIfNull(key);

        if (packedCiphertext.Length < NonceSizeBytes + TagSizeBytes)
        {
            throw new ArgumentException(
                "Ciphertext is too short to contain a valid nonce and authentication tag.",
                nameof(packedCiphertext));
        }

        int ciphertextLength = packedCiphertext.Length - NonceSizeBytes - TagSizeBytes;

        byte[] nonce = new byte[NonceSizeBytes];
        byte[] ciphertext = new byte[ciphertextLength];
        byte[] tag = new byte[TagSizeBytes];
        Buffer.BlockCopy(packedCiphertext, 0, nonce, 0, NonceSizeBytes);
        Buffer.BlockCopy(packedCiphertext, NonceSizeBytes, ciphertext, 0, ciphertextLength);
        Buffer.BlockCopy(packedCiphertext, NonceSizeBytes + ciphertextLength, tag, 0, TagSizeBytes);

        byte[] plaintext = new byte[ciphertextLength];
        try
        {
            using var aesGcm = new AesGcm(key, TagSizeBytes);
            aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);
            return plaintext;
        }
        catch (CryptographicException ex)
        {
            // Tag verification failed (wrong key or tampered/corrupted data) — never
            // return the partially-populated buffer, and never hand back the raw
            // framework exception as-is so callers get an unambiguous, documented failure mode.
            SecureMemoryHelper.Clear(plaintext);
            throw new VaultCryptographyException(
                "Decryption failed: the key is incorrect or the ciphertext has been tampered with or corrupted.",
                ex);
        }
    }
}
