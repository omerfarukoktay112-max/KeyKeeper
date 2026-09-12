using SQLite;

namespace KeyKeeper.Core.Data;

/// <summary>
/// Reads and enforces the vault's schema version, and is where future migrations get added.
/// Deliberately does not know how to derive keys, open connections, or anything else about
/// <see cref="IVaultDatabaseService"/> — it only ever operates on an already-open connection.
/// </summary>
public interface IVaultMigrationService
{
    /// <summary>The schema version this build of the app expects/produces.</summary>
    int CurrentSchemaVersion { get; }

    /// <summary>
    /// Reads <c>VaultMetadata.SchemaVersion</c> from an open connection, or <c>null</c> if the
    /// vault hasn't been created yet (no <c>VaultMetadata</c> table/row exists).
    /// </summary>
    int? ReadSchemaVersion(SQLiteConnection connection);

    /// <summary>
    /// Brings the database's schema up to <see cref="CurrentSchemaVersion"/>, applying any
    /// pending migrations in order. A no-op for a freshly created (not-yet-initialized) vault,
    /// or one already at the current version.
    /// </summary>
    /// <exception cref="VaultDatabaseException">
    /// The stored schema version is newer than <see cref="CurrentSchemaVersion"/> — this build
    /// of the app is too old to open this vault.
    /// </exception>
    void EnsureUpToDate(SQLiteConnection connection);
}
