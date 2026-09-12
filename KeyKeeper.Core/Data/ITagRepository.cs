namespace KeyKeeper.Core.Data;

/// <summary>
/// CRUD for tag *entities* themselves -- renaming a tag everywhere it's used, deleting one
/// outright, listing every tag that exists. Deliberately does not duplicate
/// <see cref="IVaultItemRepository.AddTag"/>/<see cref="IVaultItemRepository.RemoveTag"/>, which
/// already own the item-tag association (added in Phase 4, alongside item CRUD, since tagging an
/// item is naturally that repository's concern) -- <see cref="Create"/> here exists for the same
/// reason those two already create a tag on demand: so a "manage tags" UI can pre-create a tag
/// before it's ever attached to an item, not because item-tagging needed a second way to do it.
///
/// Synchronous, not <c>Task</c>-returning, matching every other Data-layer type in this codebase.
/// </summary>
public interface ITagRepository
{
    /// <exception cref="ArgumentException">A tag with this name already exists (<c>Tags.Name</c>
    /// is unique).</exception>
    TagRecord Create(string name);

    TagRecord? Get(string id);

    /// <summary>Every tag in the vault, alphabetical.</summary>
    IReadOnlyList<TagRecord> ListAll();

    /// <exception cref="KeyNotFoundException">No tag with this id exists.</exception>
    /// <exception cref="ArgumentException">Another tag already has this name.</exception>
    void Rename(string id, string newName);

    /// <summary>Deletes the tag outright -- per <c>ItemTags.TagId</c>'s <c>ON DELETE CASCADE</c>
    /// (see <c>VaultSchema</c>), it's removed from every item that had it, not just no longer
    /// creatable. A no-op if no tag with this id exists.</summary>
    void Delete(string id);

    /// <summary>Deletes every tag currently attached to zero items -- a tidy-up action for a
    /// "manage tags" screen, not something that runs automatically (a tag a user just created but
    /// hasn't attached to anything yet is still theirs to keep). Returns the number deleted.</summary>
    int DeleteUnused();
}
