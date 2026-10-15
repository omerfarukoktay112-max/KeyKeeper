namespace KeyKeeper.Core.Backup;

public enum BackupExportResultKind
{
    Success,

    /// <summary>The password given to <c>IBackupService.ExportAsync</c> didn't match the vault's
    /// own stored verifier — checked before anything is encrypted, so a typo never silently
    /// produces a backup file encrypted under the wrong key (see that method's doc comment).</summary>
    WrongPassword,
}

/// <summary>The outcome of <c>IBackupService.ExportAsync</c>. <see cref="FileBytes"/> is the
/// complete, ready-to-write .vault file and is non-null only on
/// <see cref="BackupExportResultKind.Success"/>.</summary>
public sealed record BackupExportResult(BackupExportResultKind Kind, byte[]? FileBytes, string? ErrorMessage = null)
{
    public static BackupExportResult WrongPassword { get; } =
        new(BackupExportResultKind.WrongPassword, null, CoreStrings.ExportWrongPassword);
}
