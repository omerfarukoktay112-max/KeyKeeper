namespace KeyKeeper.Core.Backup;

/// <summary>A tag as stored inside a .vault backup's decrypted payload — a direct mirror of
/// <c>KeyKeeper.Core.Data.TagRecord</c>; see <see cref="BackupFolder"/>'s doc comment for why
/// this isn't just a reuse of that type.</summary>
public sealed record BackupTag(string Id, string Name);
