namespace KeyKeeper.Core.Crypto;

/// <summary>
/// Authenticated symmetric encryption for vault data. Implementations pack the nonce,
/// ciphertext, and authentication tag into a single buffer (see
/// <see cref="AesGcmEncryptionService"/> for the exact layout).
/// </summary>
public interface IEncryptionService
{
    /// <summary>
    /// Encrypts <paramref name="plaintext"/> under <paramref name="key"/>, generating a
    /// fresh random nonce for this call. The caller owns and must clear both buffers
    /// when they're no longer needed.
    /// </summary>
    /// <returns>nonce || ciphertext || tag, packed into one buffer.</returns>
    byte[] Encrypt(byte[] plaintext, byte[] key);

    /// <summary>
    /// Decrypts a buffer produced by <see cref="Encrypt"/> under <paramref name="key"/>.
    /// </summary>
    /// <exception cref="VaultCryptographyException">
    /// The key is wrong, or the ciphertext has been corrupted/tampered with (authentication
    /// tag verification failed).
    /// </exception>
    byte[] Decrypt(byte[] packedCiphertext, byte[] key);
}
