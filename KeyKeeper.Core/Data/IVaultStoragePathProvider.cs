namespace KeyKeeper.Core.Data;

/// <summary>
/// Resolves the on-disk path for the vault database file. Implemented per-platform in the
/// MAUI head project (private, non-shared, non-backed-up app storage) so that
/// <see cref="VaultDatabaseService"/> itself never touches a platform API and stays
/// unit-testable with an in-memory/temp-directory fake.
/// </summary>
public interface IVaultStoragePathProvider
{
    /// <summary>
    /// Returns the full path to the (SQLCipher-encrypted) vault database file, creating its
    /// parent directory if necessary. Must always return the same path for the lifetime of the
    /// app install.
    /// </summary>
    string GetDatabaseFilePath();

    /// <summary>
    /// Returns the full path to the small, deliberately <em>unencrypted</em> database used by
    /// <see cref="VaultUnlockStore"/> (KDF salt/parameters, master-password verifier, lockout
    /// state) — separate from <see cref="GetDatabaseFilePath"/> because that data must be
    /// readable before the vault key exists to open the encrypted database. Same private,
    /// non-backed-up storage location as the vault database itself.
    /// </summary>
    string GetUnlockMetadataFilePath();
}
