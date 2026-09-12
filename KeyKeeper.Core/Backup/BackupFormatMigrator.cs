namespace KeyKeeper.Core.Backup;

/// <summary>
/// Format version 1 is both the current floor and ceiling — there is nothing to migrate yet.
/// This class exists, deliberately as a near-empty skeleton, so the shape of version handling is
/// settled now: when format version 2 exists (a payload shape change), add a step here following
/// the pattern <c>VaultMigrationService.EnsureUpToDate</c> already established for the live vault
/// schema, rather than redesigning this from scratch under pressure the day it's actually needed.
/// </summary>
public sealed class BackupFormatMigrator : IBackupFormatMigrator
{
    public int CurrentFormatVersion => 1;

    public void EnsureSupported(int version)
    {
        if (version > CurrentFormatVersion)
        {
            throw new UnsupportedBackupVersionException(version, CurrentFormatVersion);
        }

        // Version 1 has no predecessor, so there is nothing to do below this point yet. A future
        // version 2 would add, e.g.:
        //
        //   if (version < 2)
        //   {
        //       // transform the decrypted payload from the v1 shape to the v2 shape here
        //   }
    }
}
