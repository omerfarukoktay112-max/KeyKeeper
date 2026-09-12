namespace KeyKeeper.Core.Backup;

/// <summary>A folder as stored inside a .vault backup's decrypted payload — a direct mirror of
/// <c>KeyKeeper.Core.Data.FolderRecord</c>, kept as its own type here (rather than reusing that
/// record) so the on-disk backup shape doesn't silently change if <c>FolderRecord</c> ever does.</summary>
public sealed record BackupFolder(string Id, string Name, string? ParentFolderId);
