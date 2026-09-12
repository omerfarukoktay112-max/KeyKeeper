using KeyKeeper.Core.Data;

namespace KeyKeeper.Core.Backup;

/// <summary>
/// Renders every vault item into a single flat, plaintext CSV — every field, including
/// passwords/CVVs/license keys, appears unencrypted in the output. This is a deliberate,
/// explicitly-warned-about escape hatch (see the "CSV Olarak Dışa Aktar" flow in
/// <c>KeyKeeper.ViewModels.BackupViewModel</c>, which requires re-confirming the master password
/// before calling this), not a safe-by-default export path — unlike <see cref="IBackupService"/>'s
/// .vault format, nothing about this output is ever encrypted.
/// </summary>
public interface ICsvExportService
{
    /// <summary>Builds the CSV text (including a header row) for <paramref name="items"/>. See
    /// <c>CsvExportService</c>'s doc comment for the exact column set.</summary>
    string Export(IReadOnlyList<VaultItemRecord> items);
}
