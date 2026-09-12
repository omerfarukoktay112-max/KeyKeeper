using SQLite;

namespace KeyKeeper.Core.Data;

/// <summary>
/// Schema version 1 is both the current floor and ceiling — there is nothing to migrate yet.
/// This class exists so the *shape* of migration handling is settled now: when schema version
/// 2 exists, add a step inside <see cref="EnsureUpToDate"/> following the pattern shown in the
/// comment there, rather than redesigning this service from scratch.
/// </summary>
public sealed class VaultMigrationService : IVaultMigrationService
{
    public int CurrentSchemaVersion => 1;

    public int? ReadSchemaVersion(SQLiteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        int tableExists = connection.ExecuteScalar<int>(
            "SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name = 'VaultMetadata';");
        if (tableExists == 0)
        {
            return null;
        }

        return connection.ExecuteScalar<int?>("SELECT SchemaVersion FROM VaultMetadata WHERE Id = 1;");
    }

    public void EnsureUpToDate(SQLiteConnection connection)
    {
        int? version = ReadSchemaVersion(connection);
        if (version is null)
        {
            // No VaultMetadata row yet -- vault hasn't been created/initialized. Nothing to
            // migrate; the row gets written with SchemaVersion = CurrentSchemaVersion when the
            // vault is first created (a later phase).
            return;
        }

        if (version.Value > CurrentSchemaVersion)
        {
            throw new VaultDatabaseException(
                $"This vault's schema version ({version.Value}) is newer than this app supports " +
                $"(max {CurrentSchemaVersion}). Update the app to open it.");
        }

        // Future migrations go here, applied in order, e.g.:
        //
        //   if (version.Value < 2)
        //   {
        //       connection.Execute("ALTER TABLE VaultItems ADD COLUMN ... ;");
        //       connection.Execute("UPDATE VaultMetadata SET SchemaVersion = 2 WHERE Id = 1;");
        //       version = 2;
        //   }
        //
        // Version 1 has no predecessor, so there is nothing to do below this point yet.
    }
}
