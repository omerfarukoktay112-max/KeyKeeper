namespace KeyKeeper.Core.Data;

/// <summary>
/// Everything needed to attempt unlocking the vault with a master password, before the
/// SQLCipher-encrypted database itself can be opened. None of these values are secret on their
/// own — a KDF salt and cost parameters are meant to be public, and the verifier is a one-way
/// function of the derived key (see <c>KeyKeeper.Core.Crypto.MasterPasswordVerifier</c>) — so
/// storing them unencrypted is standard practice, not a weakened design.
/// </summary>
public sealed record VaultUnlockMetadata(
    int SchemaVersion,
    byte[] Salt,
    int Argon2MemorySizeKiB,
    int Argon2Iterations,
    int Argon2Parallelism,
    int Argon2OutputLengthBytes,
    byte[] MasterPasswordVerifier,
    DateTimeOffset CreatedAtUtc);
