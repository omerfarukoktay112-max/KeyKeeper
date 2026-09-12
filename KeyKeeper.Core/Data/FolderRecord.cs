namespace KeyKeeper.Core.Data;

/// <summary>A vault folder, as returned by <see cref="IFolderRepository"/>. <see cref="ParentFolderId"/>
/// is <c>null</c> for a top-level (root) folder.</summary>
public sealed record FolderRecord(string Id, string Name, string? ParentFolderId);
