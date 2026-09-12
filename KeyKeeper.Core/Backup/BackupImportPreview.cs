namespace KeyKeeper.Core.Backup;

/// <summary>
/// A successfully decrypted, not-yet-applied import: the counts a confirmation dialog shows the
/// user before an irreversible replace ("N öğe, M klasör, K etiket içe aktarılacak — mevcut
/// kasanızın tüm içeriği silinecek"), plus the decrypted <see cref="Payload"/> itself, held only
/// in memory between <c>IBackupService.PrepareImportAsync</c> and
/// <c>IBackupService.CommitImportAsync</c>.
///
/// MVP scope decision — replace, not merge: <see cref="CommitImportAsync"/>-time behavior (see
/// <c>BackupService</c>) always <em>replaces</em> every folder/tag/item currently in the vault
/// with what this preview holds, rather than merging the two sets. A merge would need a real
/// conflict-resolution design (same id in both, same title but different fields, a folder that
/// exists in one but not the other, ...) that nothing in this phase's scope asked for; replace is
/// simple, predictable, and matches what most users actually want from "restore a backup"
/// (recover exactly what a backup captured) rather than "combine two vaults" (a plausible future
/// feature, deliberately not this one).
/// </summary>
public sealed record BackupImportPreview(int FolderCount, int TagCount, int ItemCount, VaultBackupPayload Payload);
