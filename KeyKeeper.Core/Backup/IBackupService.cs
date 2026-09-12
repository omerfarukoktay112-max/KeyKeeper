namespace KeyKeeper.Core.Backup;

/// <summary>
/// Whole-vault backup/restore to and from a single portable, versioned .vault file (see
/// <see cref="VaultBackupFile"/> for the exact on-disk layout and <see cref="VaultBackupHeader"/>
/// for why its header must stay unencrypted). Operates on the currently unlocked vault —
/// exporting reads through it, importing replaces its contents — so every method here requires
/// the vault to already be open, same precondition as
/// <c>KeyKeeper.Core.Data.IVaultItemRepository</c>.
///
/// Import is a two-step, confirm-before-destroy flow: <see cref="PrepareImportAsync"/> fully
/// validates the password and file — so a wrong password, a corrupt file, and an unsupported
/// format version are each reported distinctly, before anything about the current vault changes
/// — and only <see cref="CommitImportAsync"/> actually replaces the vault's data. This lets a UI
/// show the user exactly what's about to happen (item/folder/tag counts) and get explicit
/// confirmation for what is otherwise a silent, irreversible overwrite.
/// </summary>
public interface IBackupService
{
    /// <summary>
    /// Builds a complete .vault backup of the currently unlocked vault's folders, tags, and
    /// items, encrypted under a key derived from <paramref name="masterPassword"/> with a fresh,
    /// independent salt — deliberately not the live vault's own KDF salt/key (see
    /// <c>BackupService</c>'s doc comment on why reusing it would be a key-hygiene regression).
    /// <paramref name="masterPassword"/> is verified against the vault's own stored verifier
    /// first, so a typo is reported as <see cref="BackupExportResultKind.WrongPassword"/> instead
    /// of silently producing a backup nothing can ever decrypt correctly.
    /// </summary>
    /// <exception cref="InvalidOperationException">The vault is locked.</exception>
    Task<BackupExportResult> ExportAsync(char[] masterPassword);

    /// <summary>
    /// Re-derives a key from <paramref name="masterPassword"/> and the <em>live</em> vault's own
    /// stored salt/parameters and checks it against the stored verifier, without exporting,
    /// importing, or touching any file. Exists so a "re-enter your master password to confirm"
    /// gate (the CSV export warning screen; see <c>KeyKeeper.Core.Backup.ICsvExportService</c>)
    /// can require a genuine password match rather than merely a non-empty field.
    /// </summary>
    /// <exception cref="InvalidOperationException">The vault has not been initialized yet.</exception>
    Task<bool> VerifyMasterPasswordAsync(char[] masterPassword);

    /// <summary>
    /// Reads and validates a .vault file at <paramref name="filePath"/> against
    /// <paramref name="masterPassword"/>: a wrong password, a corrupt/foreign file, and an
    /// unsupported future format version are each reported as a distinct
    /// <see cref="BackupImportResultKind"/> — without touching the current vault's data. Call
    /// <see cref="CommitImportAsync"/> with the returned <see cref="BackupImportResult.Preview"/>
    /// only after the user has explicitly confirmed the irreversible replace.
    /// </summary>
    Task<BackupImportResult> PrepareImportAsync(string filePath, char[] masterPassword);

    /// <summary>
    /// Replaces every folder, tag, and item currently in the vault with what
    /// <paramref name="preview"/> holds, re-encrypting each item's payload under the currently
    /// open vault's own key (never the backup's own key — see <c>BackupService</c>). Irreversible:
    /// the caller must have already confirmed this with the user (see
    /// <see cref="BackupImportPreview"/>'s doc comment on why this is a replace, not a merge).
    /// </summary>
    /// <exception cref="InvalidOperationException">The vault is locked.</exception>
    Task CommitImportAsync(BackupImportPreview preview);
}
