using KeyKeeper.Core.Data;
using SQLite;

namespace KeyKeeper.Core.Tests.Data;

[Collection(nameof(VaultDatabaseTestCollection))]
public class VaultSchemaTests
{
    private static SQLiteConnection OpenTempConnection(out string path)
    {
        path = Path.Combine(Path.GetTempPath(), $"keykeeper-test-{Guid.NewGuid():N}.db3");
        return new SQLiteConnection(path);
    }

    [Fact]
    public void EnsureCreated_CreatesAllExpectedTables()
    {
        using SQLiteConnection connection = OpenTempConnection(out string path);
        try
        {
            VaultSchema.EnsureCreated(connection);

            HashSet<string> tableNames = connection
                .Query<TableNameRow>("SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%';")
                .Select(r => r.Name)
                .ToHashSet(StringComparer.Ordinal);

            Assert.Contains("VaultMetadata", tableNames);
            Assert.Contains("Folders", tableNames);
            Assert.Contains("VaultItems", tableNames);
            Assert.Contains("Tags", tableNames);
            Assert.Contains("ItemTags", tableNames);
        }
        finally
        {
            connection.Close();
            File.Delete(path);
        }
    }

    [Fact]
    public void EnsureCreated_CalledTwiceOnSameConnection_DoesNotThrow()
    {
        using SQLiteConnection connection = OpenTempConnection(out string path);
        try
        {
            VaultSchema.EnsureCreated(connection);
            Exception? exception = Record.Exception(() => VaultSchema.EnsureCreated(connection));

            Assert.Null(exception);
        }
        finally
        {
            connection.Close();
            File.Delete(path);
        }
    }

    [Fact]
    public void EnsureCreated_CalledAgainOnAFreshConnectionToTheSameFile_DoesNotThrow()
    {
        // Simulates the real scenario: the app closes and reopens the vault across launches.
        string path = Path.Combine(Path.GetTempPath(), $"keykeeper-test-{Guid.NewGuid():N}.db3");
        try
        {
            using (var first = new SQLiteConnection(path))
            {
                VaultSchema.EnsureCreated(first);
            }

            using var second = new SQLiteConnection(path);
            Exception? exception = Record.Exception(() => VaultSchema.EnsureCreated(second));

            Assert.Null(exception);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ForeignKeys_WhenEnabled_RejectOrphanVaultItem()
    {
        using SQLiteConnection connection = OpenTempConnection(out string path);
        try
        {
            connection.Execute("PRAGMA foreign_keys = ON;");
            VaultSchema.EnsureCreated(connection);

            Assert.Throws<SQLiteException>(() => connection.Execute(
                "INSERT INTO VaultItems (Id, ItemType, FolderId, IsFavorite, CreatedAtUtc, UpdatedAtUtc, EncryptedPayload) " +
                "VALUES ('item-1', 0, 'no-such-folder', 0, 0, 0, x'00');"));
        }
        finally
        {
            connection.Close();
            File.Delete(path);
        }
    }

    private sealed class TableNameRow
    {
        public string Name { get; set; } = string.Empty;
    }
}
