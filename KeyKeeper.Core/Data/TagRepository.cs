using SQLite;

namespace KeyKeeper.Core.Data;

/// <summary><see cref="ITagRepository"/> backed by the <c>Tags</c>/<c>ItemTags</c> tables (see
/// <see cref="VaultSchema"/>).</summary>
public sealed class TagRepository : ITagRepository
{
    private readonly IVaultDatabaseService _vaultDatabase;

    public TagRepository(IVaultDatabaseService vaultDatabase)
    {
        _vaultDatabase = vaultDatabase ?? throw new ArgumentNullException(nameof(vaultDatabase));
    }

    public TagRecord Create(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        SQLiteConnection connection = _vaultDatabase.GetOpenConnectionOrThrow();
        if (NameInUse(connection, name, excludingId: null))
        {
            throw new ArgumentException($"A tag named '{name}' already exists.", nameof(name));
        }

        string id = Guid.NewGuid().ToString();
        connection.Execute("INSERT INTO Tags (Id, Name) VALUES (?, ?);", id, name);
        return new TagRecord(id, name);
    }

    public TagRecord? Get(string id)
    {
        ArgumentNullException.ThrowIfNull(id);

        SQLiteConnection connection = _vaultDatabase.GetOpenConnectionOrThrow();
        TagRow? row = connection.Query<TagRow>("SELECT * FROM Tags WHERE Id = ?;", id).FirstOrDefault();
        return row is null ? null : new TagRecord(row.Id, row.Name);
    }

    public IReadOnlyList<TagRecord> ListAll()
    {
        SQLiteConnection connection = _vaultDatabase.GetOpenConnectionOrThrow();
        return connection.Query<TagRow>("SELECT * FROM Tags ORDER BY Name;")
            .Select(row => new TagRecord(row.Id, row.Name))
            .ToList();
    }

    public void Rename(string id, string newName)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);

        SQLiteConnection connection = _vaultDatabase.GetOpenConnectionOrThrow();
        if (connection.ExecuteScalar<int>("SELECT COUNT(*) FROM Tags WHERE Id = ?;", id) == 0)
        {
            throw new KeyNotFoundException($"No tag with id '{id}' exists.");
        }

        if (NameInUse(connection, newName, excludingId: id))
        {
            throw new ArgumentException($"A tag named '{newName}' already exists.", nameof(newName));
        }

        connection.Execute("UPDATE Tags SET Name = ? WHERE Id = ?;", newName, id);
    }

    public void Delete(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        _vaultDatabase.GetOpenConnectionOrThrow().Execute("DELETE FROM Tags WHERE Id = ?;", id);
    }

    public int DeleteUnused()
    {
        SQLiteConnection connection = _vaultDatabase.GetOpenConnectionOrThrow();
        return connection.Execute(
            """
            DELETE FROM Tags WHERE Id NOT IN (SELECT DISTINCT TagId FROM ItemTags);
            """);
    }

    private static bool NameInUse(SQLiteConnection connection, string name, string? excludingId) =>
        excludingId is null
            ? connection.ExecuteScalar<int>("SELECT COUNT(*) FROM Tags WHERE Name = ?;", name) > 0
            : connection.ExecuteScalar<int>("SELECT COUNT(*) FROM Tags WHERE Name = ? AND Id != ?;", name, excludingId) > 0;

    private sealed class TagRow
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
}
