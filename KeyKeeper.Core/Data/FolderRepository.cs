using SQLite;

namespace KeyKeeper.Core.Data;

/// <summary><see cref="IFolderRepository"/> backed by the <c>Folders</c> table (see <see cref="VaultSchema"/>).</summary>
public sealed class FolderRepository : IFolderRepository
{
    private readonly IVaultDatabaseService _vaultDatabase;

    public FolderRepository(IVaultDatabaseService vaultDatabase)
    {
        _vaultDatabase = vaultDatabase ?? throw new ArgumentNullException(nameof(vaultDatabase));
    }

    public FolderRecord Create(string name, string? parentFolderId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        SQLiteConnection connection = _vaultDatabase.GetOpenConnectionOrThrow();

        if (parentFolderId is not null)
        {
            int parentExists = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM Folders WHERE Id = ?;", parentFolderId);
            if (parentExists == 0)
            {
                throw new KeyNotFoundException($"No parent folder with id '{parentFolderId}' exists.");
            }
        }

        string id = Guid.NewGuid().ToString();
        connection.Execute("INSERT INTO Folders (Id, Name, ParentFolderId) VALUES (?, ?, ?);", id, name, parentFolderId);
        return new FolderRecord(id, name, parentFolderId);
    }

    public FolderRecord? Get(string id)
    {
        ArgumentNullException.ThrowIfNull(id);

        SQLiteConnection connection = _vaultDatabase.GetOpenConnectionOrThrow();
        FolderRow? row = connection.Query<FolderRow>("SELECT * FROM Folders WHERE Id = ?;", id).FirstOrDefault();
        return row is null ? null : new FolderRecord(row.Id, row.Name, row.ParentFolderId);
    }

    public IReadOnlyList<FolderRecord> ListAll()
    {
        SQLiteConnection connection = _vaultDatabase.GetOpenConnectionOrThrow();
        return connection.Query<FolderRow>("SELECT * FROM Folders ORDER BY Name;")
            .Select(row => new FolderRecord(row.Id, row.Name, row.ParentFolderId))
            .ToList();
    }

    public void Rename(string id, string newName)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);

        SQLiteConnection connection = _vaultDatabase.GetOpenConnectionOrThrow();
        int rows = connection.Execute("UPDATE Folders SET Name = ? WHERE Id = ?;", newName, id);
        if (rows == 0)
        {
            throw new KeyNotFoundException($"No folder with id '{id}' exists.");
        }
    }

    public void Delete(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        _vaultDatabase.GetOpenConnectionOrThrow().Execute("DELETE FROM Folders WHERE Id = ?;", id);
    }

    private sealed class FolderRow
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? ParentFolderId { get; set; }
    }
}
