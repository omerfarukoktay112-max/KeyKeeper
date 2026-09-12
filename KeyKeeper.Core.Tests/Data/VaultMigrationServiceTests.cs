using KeyKeeper.Core.Data;
using SQLite;

namespace KeyKeeper.Core.Tests.Data;

[Collection(nameof(VaultDatabaseTestCollection))]
public class VaultMigrationServiceTests
{
    private readonly VaultMigrationService _sut = new();

    private static SQLiteConnection OpenTempConnection(out string path)
    {
        path = Path.Combine(Path.GetTempPath(), $"keykeeper-test-{Guid.NewGuid():N}.db3");
        return new SQLiteConnection(path);
    }

    private static void InsertMetadataRow(SQLiteConnection connection, int schemaVersion)
    {
        connection.Execute(
            "INSERT INTO VaultMetadata (Id, SchemaVersion, CreatedAtUtc) VALUES (1, ?, 0);",
            schemaVersion);
    }

    [Fact]
    public void ReadSchemaVersion_NoVaultMetadataTableYet_ReturnsNull()
    {
        using SQLiteConnection connection = OpenTempConnection(out string path);
        try
        {
            Assert.Null(_sut.ReadSchemaVersion(connection));
        }
        finally
        {
            connection.Close();
            File.Delete(path);
        }
    }

    [Fact]
    public void ReadSchemaVersion_MetadataRowPresent_ReturnsStoredVersion()
    {
        using SQLiteConnection connection = OpenTempConnection(out string path);
        try
        {
            VaultSchema.EnsureCreated(connection);
            InsertMetadataRow(connection, schemaVersion: 1);

            Assert.Equal(1, _sut.ReadSchemaVersion(connection));
        }
        finally
        {
            connection.Close();
            File.Delete(path);
        }
    }

    [Fact]
    public void EnsureUpToDate_NoVaultMetadataYet_DoesNotThrow()
    {
        using SQLiteConnection connection = OpenTempConnection(out string path);
        try
        {
            Exception? exception = Record.Exception(() => _sut.EnsureUpToDate(connection));

            Assert.Null(exception);
        }
        finally
        {
            connection.Close();
            File.Delete(path);
        }
    }

    [Fact]
    public void EnsureUpToDate_AtCurrentVersion_DoesNotThrow()
    {
        using SQLiteConnection connection = OpenTempConnection(out string path);
        try
        {
            VaultSchema.EnsureCreated(connection);
            InsertMetadataRow(connection, schemaVersion: _sut.CurrentSchemaVersion);

            Exception? exception = Record.Exception(() => _sut.EnsureUpToDate(connection));

            Assert.Null(exception);
        }
        finally
        {
            connection.Close();
            File.Delete(path);
        }
    }

    [Fact]
    public void EnsureUpToDate_VersionNewerThanSupported_ThrowsVaultDatabaseException()
    {
        using SQLiteConnection connection = OpenTempConnection(out string path);
        try
        {
            VaultSchema.EnsureCreated(connection);
            InsertMetadataRow(connection, schemaVersion: _sut.CurrentSchemaVersion + 1);

            Assert.Throws<VaultDatabaseException>(() => _sut.EnsureUpToDate(connection));
        }
        finally
        {
            connection.Close();
            File.Delete(path);
        }
    }
}
