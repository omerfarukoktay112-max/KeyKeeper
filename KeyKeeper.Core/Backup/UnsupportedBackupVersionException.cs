namespace KeyKeeper.Core.Backup;

/// <summary>
/// Thrown when a .vault file's <see cref="VaultBackupHeader.FormatVersion"/> is newer than this
/// build of the app supports — mirrors <c>KeyKeeper.Core.Data.VaultMigrationService</c>'s
/// identical schema-version guard, for the identical reason: a backup made by a future version
/// of the app may use a payload shape this build doesn't know how to read, so refuse it loudly
/// instead of guessing.
/// </summary>
public sealed class UnsupportedBackupVersionException : Exception
{
    public int FoundVersion { get; }

    public int MaxSupportedVersion { get; }

    public UnsupportedBackupVersionException(int foundVersion, int maxSupportedVersion)
        : base(
            $"This backup's format version ({foundVersion}) is newer than this app supports " +
            $"(max {maxSupportedVersion}). Update the app to restore it.")
    {
        FoundVersion = foundVersion;
        MaxSupportedVersion = maxSupportedVersion;
    }
}
