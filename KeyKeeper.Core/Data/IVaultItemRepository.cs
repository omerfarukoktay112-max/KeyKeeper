using KeyKeeper.Core.Models;

namespace KeyKeeper.Core.Data;

/// <summary>
/// CRUD for vault items, favorite/folder/tag management, and type-filtered listing. Every
/// method requires the vault to be unlocked (see <c>KeyKeeper.Core.Auth.IAuthenticationService</c>)
/// — item payloads are encrypted/decrypted via <c>IAuthenticationService.EncryptWithVaultKey</c>/
/// <c>DecryptWithVaultKey</c>, so an <see cref="InvalidOperationException"/> from those propagates
/// out of every method here when called on a locked vault.
///
/// Synchronous, not <c>Task</c>-returning, matching every other Data-layer type in this codebase
/// (<c>VaultDatabaseService</c>, <c>VaultUnlockStore</c>, <c>VaultSchema</c>) — these are local
/// SQLite operations on an already-open connection, not the kind of slow I/O that warrants an
/// async signature; <c>KeyKeeper.Core.Auth</c> is the layer that wraps genuinely slow work
/// (Argon2id) in <c>Task.Run</c>, not this one.
/// </summary>
public interface IVaultItemRepository
{
    /// <summary>Encrypts and inserts a new item, returning the created record (with a fresh
    /// <see cref="VaultItemRecord.Id"/>, no folder unless <paramref name="folderId"/> is given,
    /// not a favorite, and no tags).</summary>
    VaultItemRecord Create<TPayload>(TPayload payload, string? folderId = null)
        where TPayload : IVaultItemPayload;

    /// <summary>Returns the decrypted item, or <c>null</c> if no item with this id exists.</summary>
    VaultItemRecord? Get(string id);

    /// <summary>Returns every item (optionally filtered to one <see cref="VaultItemType"/>),
    /// decrypted, newest-updated first.</summary>
    IReadOnlyList<VaultItemRecord> List(VaultItemType? type = null);

    /// <summary>Re-encrypts and replaces an existing item's payload (its folder/favorite/tags
    /// are untouched). <paramref name="payload"/>'s <see cref="IVaultItemPayload.ItemType"/> must
    /// match the item's existing, immutable type.</summary>
    /// <exception cref="ArgumentException"><paramref name="payload"/>'s item type doesn't match
    /// the stored item's type.</exception>
    /// <exception cref="KeyNotFoundException">No item with this id exists.</exception>
    VaultItemRecord Update<TPayload>(string id, TPayload payload)
        where TPayload : IVaultItemPayload;

    /// <summary>Deletes an item (and, via <c>ON DELETE CASCADE</c>, its tag associations). A
    /// no-op if no item with this id exists.</summary>
    void Delete(string id);

    void SetFavorite(string id, bool isFavorite);

    /// <summary><paramref name="folderId"/> of <c>null</c> removes the item from any folder.</summary>
    void SetFolder(string id, string? folderId);

    /// <summary>Adds <paramref name="tagName"/> to the item, creating the tag if it doesn't
    /// already exist. A no-op if the item is already tagged with it.</summary>
    void AddTag(string id, string tagName);

    /// <summary>Removes <paramref name="tagName"/> from the item, if present. A no-op otherwise
    /// (including if the tag itself doesn't exist).</summary>
    void RemoveTag(string id, string tagName);
}
