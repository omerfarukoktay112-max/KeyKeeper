using SQLite;

namespace KeyKeeper.Core.Data;

/// <summary>
/// Owns the SQLCipher-encrypted vault database connection: opening it with a raw 256-bit key,
/// ensuring the schema exists, and closing it. Does not derive keys itself — callers get the
/// key from <c>KeyKeeper.Core.Crypto.IKeyDerivationService</c> (master password flow) or from
/// wherever the biometric-unlock flow decrypts it (see <c>KeyKeeper.Core.Storage.IWrappedKeyStorage</c>).
/// </summary>
public interface IVaultDatabaseService : IDisposable
{
    /// <summary>Whether a connection is currently open.</summary>
    bool IsOpen { get; }

    /// <summary>
    /// Opens (creating if necessary) the vault database with the given 256-bit key and
    /// ensures its schema is up to date. Calling this again while already open returns the
    /// existing connection without re-keying.
    /// </summary>
    /// <exception cref="ArgumentException">The key is not exactly 32 bytes.</exception>
    /// <exception cref="VaultDatabaseException">
    /// The key is wrong, the file is corrupted/not a valid database, or its schema version is
    /// newer than this app supports.
    /// </exception>
    SQLiteConnection Open(byte[] key);

    /// <summary>Closes the connection, if open. Safe to call when already closed.</summary>
    void Close();

    /// <summary>
    /// Returns the already-open connection (Phase 4: <c>IVaultItemRepository</c> uses this to
    /// run item queries without needing the vault key itself -- unlike <see cref="Open"/>, which
    /// both keys and opens the connection).
    /// </summary>
    /// <exception cref="InvalidOperationException">The vault database isn't currently open.</exception>
    SQLiteConnection GetOpenConnectionOrThrow();
}
