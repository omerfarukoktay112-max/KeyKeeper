namespace KeyKeeper.Core.Backup;

public enum BackupImportResultKind
{
    Success,

    /// <summary>The file parsed fine, but decrypting its payload with the given password failed
    /// — either the password is wrong, or the payload itself was tampered with/corrupted (AES-GCM
    /// can't tell those apart; see <c>KeyKeeper.Core.Crypto.VaultCryptographyException</c>).</summary>
    WrongPassword,

    /// <summary>Not a structurally valid .vault file at all, or too badly truncated/damaged to
    /// even reach the point of trying a password — see <see cref="VaultBackupFormatException"/>.</summary>
    CorruptFile,

    /// <summary>A structurally valid file whose <c>FormatVersion</c> is newer than this app
    /// supports — see <see cref="UnsupportedBackupVersionException"/>.</summary>
    UnsupportedVersion,
}

/// <summary>
/// The outcome of <c>IBackupService.PrepareImportAsync</c>. Deliberately a result record rather
/// than exceptions for every failure path (matching <c>KeyKeeper.Core.Auth.AuthenticationResult</c>'s
/// precedent) — wrong password, a corrupt file, and an unsupported version are all *expected*,
/// distinctly-messaged outcomes a UI needs to branch on, not exceptional program states.
/// <see cref="Preview"/> is non-null only on <see cref="BackupImportResultKind.Success"/>; every
/// other kind carries a Turkish, user-facing <see cref="ErrorMessage"/> instead.
/// </summary>
public sealed record BackupImportResult(BackupImportResultKind Kind, BackupImportPreview? Preview, string? ErrorMessage)
{
    public static BackupImportResult Success(BackupImportPreview preview) =>
        new(BackupImportResultKind.Success, preview, null);

    public static BackupImportResult WrongPassword() =>
        new(BackupImportResultKind.WrongPassword, null, CoreStrings.ImportWrongPassword);

    public static BackupImportResult CorruptFile(string message) =>
        new(BackupImportResultKind.CorruptFile, null, message);

    public static BackupImportResult UnsupportedVersion(string message) =>
        new(BackupImportResultKind.UnsupportedVersion, null, message);
}
