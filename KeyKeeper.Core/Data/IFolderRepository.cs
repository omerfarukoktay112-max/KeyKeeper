namespace KeyKeeper.Core.Data;

/// <summary>
/// CRUD for vault folders. Every method requires the vault to be unlocked, same as
/// <see cref="IVaultItemRepository"/> -- folder names aren't app-layer-encrypted (see
/// <c>VaultSchema</c>'s doc comment for why that's fine), but the connection itself is only
/// available while unlocked (<see cref="IVaultDatabaseService.GetOpenConnectionOrThrow"/>).
///
/// Synchronous, not <c>Task</c>-returning, matching every other Data-layer type in this codebase.
/// </summary>
public interface IFolderRepository
{
    /// <summary>Creates a new folder, optionally nested under <paramref name="parentFolderId"/>.</summary>
    /// <exception cref="KeyNotFoundException"><paramref name="parentFolderId"/> is given but no
    /// such folder exists.</exception>
    FolderRecord Create(string name, string? parentFolderId = null);

    FolderRecord? Get(string id);

    /// <summary>Every folder in the vault, flat (not just one level) -- callers build whatever
    /// tree/indented display they need from <see cref="FolderRecord.ParentFolderId"/> themselves
    /// (see <c>KeyKeeper.Services.FolderTreeFlattener</c> in the MAUI head project).</summary>
    IReadOnlyList<FolderRecord> ListAll();

    /// <exception cref="KeyNotFoundException">No folder with this id exists.</exception>
    void Rename(string id, string newName);

    /// <summary>
    /// Deletes a folder. Per the schema's own foreign-key actions (see <c>VaultSchema</c>):
    /// every descendant folder is cascade-deleted too (<c>Folders.ParentFolderId</c> is
    /// <c>ON DELETE CASCADE</c>), and every item that was directly inside the deleted folder or
    /// any of its now-deleted descendants becomes folderless -- <c>VaultItems.FolderId</c> is
    /// <c>ON DELETE SET NULL</c>, not <c>CASCADE</c>. Items are data the user typed a master
    /// password to protect; a folder is just organizational metadata, so deleting one can never
    /// destroy an item or force the user to relocate items first -- they simply reappear
    /// unfiled. A no-op if no folder with this id exists.
    /// </summary>
    void Delete(string id);
}
