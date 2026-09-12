namespace KeyKeeper.Core.Backup;

/// <summary>
/// Format-version compatibility check for .vault backup files — the exact same "settle the shape
/// of migration handling now, before there's anything to migrate" role
/// <c>KeyKeeper.Core.Data.IVaultMigrationService</c> plays for the live vault database schema.
/// <see cref="VaultBackupHeader.FormatVersion"/> is written into every backup specifically so
/// this check is possible: if a later phase changes the encrypted payload's JSON shape (a field
/// renamed, a new required field, ...), bump <see cref="CurrentFormatVersion"/> and add a
/// version-aware transform inside <see cref="EnsureSupported"/> (or, once payload transforms are
/// actually needed, a dedicated migrate step called from <c>BackupService</c>) — old backups
/// stay readable instead of silently misinterpreted.
/// </summary>
public interface IBackupFormatMigrator
{
    /// <summary>The .vault format version this build writes and can read.</summary>
    int CurrentFormatVersion { get; }

    /// <summary>
    /// Throws if <paramref name="version"/> is newer than <see cref="CurrentFormatVersion"/>. A
    /// no-op for any version this build already knows how to read.
    /// </summary>
    /// <exception cref="UnsupportedBackupVersionException"><paramref name="version"/> is newer
    /// than <see cref="CurrentFormatVersion"/>.</exception>
    void EnsureSupported(int version);
}
