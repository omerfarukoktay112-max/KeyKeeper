using KeyKeeper.Core.Data;

namespace KeyKeeper.Services;

/// <summary>One folder as shown in a flattened, indented list -- a Picker or a simple filter list,
/// not a real expand/collapse tree control (MAUI has no built-in TreeView; a flat indented list is
/// the pragmatic equivalent for the handful of folders a personal vault realistically has).</summary>
public sealed record IndentedFolder(string Id, string Name, int Depth)
{
    /// <summary>"— " repeated per <see cref="Depth"/>, prefixed onto <see cref="Name"/> — e.g. a
    /// depth-2 folder reads "— — Acme Corp".</summary>
    public string DisplayName => Depth == 0 ? Name : $"{string.Concat(Enumerable.Repeat("— ", Depth))}{Name}";
}

/// <summary>
/// Flattens <see cref="IFolderRepository.ListAll"/>'s flat-but-parented result into a
/// depth-first, indented display order (root folders first, each immediately followed by its own
/// descendants) -- shared by every place that shows the folder hierarchy: <c>VaultListPage</c>'s
/// filter area, <c>FolderManagementPage</c>, and each item form's folder picker.
/// </summary>
public static class FolderTreeFlattener
{
    public static IReadOnlyList<IndentedFolder> Flatten(IReadOnlyList<FolderRecord> allFolders)
    {
        // Dictionary<TKey, TValue> rejects a null key outright for any reference-type TKey, at
        // every call site (ContainsKey/TryGetValue/the indexer) -- regardless of TKey's nullable
        // annotation, which is a compile-time-only hint and changes no runtime behavior. Root
        // folders (ParentFolderId == null) are therefore handled as their own separate list, not
        // as "the group whose key happens to be null" -- the dictionary below only ever holds
        // keys for folders that are themselves someone's parent, i.e. never null.
        var byParent = allFolders
            .Where(f => f.ParentFolderId is not null)
            .GroupBy(f => f.ParentFolderId!)
            .ToDictionary(g => g.Key, g => g.OrderBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase).ToList());

        List<FolderRecord> roots = allFolders
            .Where(f => f.ParentFolderId is null)
            .OrderBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var result = new List<IndentedFolder>();
        AppendChildren(roots, 0);
        return result;

        void AppendChildren(List<FolderRecord> children, int depth)
        {
            foreach (FolderRecord folder in children)
            {
                result.Add(new IndentedFolder(folder.Id, folder.Name, depth));
                if (byParent.TryGetValue(folder.Id, out List<FolderRecord>? grandchildren))
                {
                    AppendChildren(grandchildren, depth + 1);
                }
            }
        }
    }
}
