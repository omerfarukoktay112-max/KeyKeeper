using KeyKeeper.Core.Data;

namespace KeyKeeper.Services;

/// <summary>
/// Resolves both vault storage file paths to the app's private, non-shared storage —
/// <c>FileSystem.AppDataDirectory</c> maps to <c>Context.FilesDir</c> on Android and to
/// <c>Library/Application Support/&lt;bundle id&gt;</c> on iOS, neither of which is
/// accessible to other apps.
///
/// Not backed up:
/// - Android: <c>AndroidManifest.xml</c> sets <c>android:allowBackup="false"</c>
///   app-wide, which is sufficient on its own (Auto Backup and, on Android 12+, cloud
///   backup/device transfer are governed by that flag regardless of any
///   <c>dataExtractionRules</c>) — no additional per-file handling needed here.
/// - iOS: <c>Library/</c> is already excluded from the classic iTunes/Finder backup, but NOT
///   from iCloud backup by default. We explicitly mark both database files (and their SQLite
///   WAL/SHM/journal sidecar files, if present) with <c>NSURLIsExcludedFromBackupKey</c> so
///   neither ever leaves the device via iCloud either — this applies just as much to the
///   unencrypted unlock-metadata file as to the encrypted vault database itself.
/// </summary>
public sealed class MauiVaultStoragePathProvider : IVaultStoragePathProvider
{
    private const string DatabaseFileName = "vault.db3";
    private const string UnlockMetadataFileName = "vault-unlock.db3";

    public string GetDatabaseFilePath() => ResolvePath(DatabaseFileName);

    public string GetUnlockMetadataFilePath() => ResolvePath(UnlockMetadataFileName);

    private static string ResolvePath(string fileName)
    {
        Directory.CreateDirectory(FileSystem.AppDataDirectory);
        string path = Path.Combine(FileSystem.AppDataDirectory, fileName);

#if IOS
        ExcludeFromBackup(path);
#endif

        return path;
    }

#if IOS
    private static void ExcludeFromBackup(string filePath)
    {
        foreach (string suffix in new[] { "", "-wal", "-shm", "-journal" })
        {
            string candidate = filePath + suffix;
            if (File.Exists(candidate))
            {
                Foundation.NSFileManager.SetSkipBackupAttribute(candidate, true);
            }
        }
    }
#endif
}
