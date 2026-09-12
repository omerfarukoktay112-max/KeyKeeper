using System.Security.Cryptography;

namespace KeyKeeper.Core.Crypto;

/// <summary>
/// Thrown when a vault cryptographic operation fails in a security-relevant way —
/// e.g. AES-GCM authentication tag verification failed because the key was wrong or the
/// ciphertext was tampered with/corrupted. Distinct from argument-validation exceptions,
/// which indicate a programming error rather than a decryption failure.
/// </summary>
public sealed class VaultCryptographyException : CryptographicException
{
    public VaultCryptographyException(string message)
        : base(message)
    {
    }

    public VaultCryptographyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
