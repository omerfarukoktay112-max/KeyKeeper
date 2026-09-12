namespace KeyKeeper.Core.Backup;

/// <summary>
/// The plaintext (unencrypted) header written at the start of every .vault file — everything a
/// future restore needs in order to derive the decryption key from a re-entered master password,
/// short of the password itself: the format version (see <see cref="IBackupFormatMigrator"/>),
/// the Argon2id cost parameters, and the salt.
///
/// None of this is secret on its own, the same reasoning
/// <c>KeyKeeper.Core.Data.VaultUnlockMetadata</c> already documents for the live vault's own
/// unlock metadata: a KDF salt and cost parameters are meant to be public, and without the
/// correct master password an attacker who only has this header can't derive the key that
/// decrypts <see cref="VaultBackupFile"/>'s payload. <see cref="Salt"/> here is deliberately
/// independent from the live vault's own salt — see <c>BackupService.ExportAsync</c>'s doc
/// comment for why reusing the vault's key material for a portable file would be a key-hygiene
/// regression.
/// </summary>
public sealed record VaultBackupHeader(
    int FormatVersion,
    byte[] Salt,
    int Argon2MemorySizeKiB,
    int Argon2Iterations,
    int Argon2Parallelism,
    int Argon2OutputLengthBytes,
    DateTimeOffset CreatedAtUtc);
