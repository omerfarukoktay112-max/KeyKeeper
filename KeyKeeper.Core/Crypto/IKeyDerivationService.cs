namespace KeyKeeper.Core.Crypto;

/// <summary>
/// Derives a symmetric key from a user's master password. Implementations must never
/// persist or log the master password, and callers own the lifetime of the returned key
/// (clear it with <see cref="SecureMemoryHelper"/> once it's no longer needed).
/// </summary>
public interface IKeyDerivationService
{
    /// <summary>
    /// Derives a key from <paramref name="masterPassword"/> and <paramref name="salt"/>.
    /// The caller retains ownership of <paramref name="masterPassword"/> and is responsible
    /// for clearing it after this call and after any other use.
    /// </summary>
    /// <param name="masterPassword">The master password, as characters (never a string).</param>
    /// <param name="salt">A unique salt, at least <see cref="Argon2Constants.MinSaltSizeBytes"/> bytes.</param>
    /// <param name="parameters">Argon2id cost parameters to use for this derivation.</param>
    /// <returns>The derived key. The caller owns and must clear this buffer.</returns>
    byte[] DeriveKey(char[] masterPassword, byte[] salt, Argon2Parameters parameters);

    /// <summary>
    /// Generates a new cryptographically secure random salt, suitable for use with
    /// <see cref="DeriveKey"/>. Never reuse a salt across two different derivations.
    /// </summary>
    /// <param name="sizeBytes">Salt size in bytes; must be at least <see cref="Argon2Constants.MinSaltSizeBytes"/>.</param>
    byte[] GenerateSalt(int sizeBytes = Argon2Constants.MinSaltSizeBytes);
}
