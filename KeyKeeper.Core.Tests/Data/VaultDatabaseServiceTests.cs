using KeyKeeper.Core.Data;

namespace KeyKeeper.Core.Tests.Data;

[Collection(nameof(VaultDatabaseTestCollection))]
public class VaultDatabaseServiceTests
{
    private sealed class TempPathProvider : IVaultStoragePathProvider
    {
        public string Directory { get; } = Path.Combine(Path.GetTempPath(), $"keykeeper-test-{Guid.NewGuid():N}");

        public string GetDatabaseFilePath() => Path.Combine(Directory, "vault.db3");

        public string GetUnlockMetadataFilePath() => Path.Combine(Directory, "vault-unlock.db3");

        public void Cleanup()
        {
            if (System.IO.Directory.Exists(Directory))
            {
                System.IO.Directory.Delete(Directory, recursive: true);
            }
        }
    }

    private static byte[] ValidKey() => new byte[VaultDatabaseService.RequiredKeyLengthBytes];

    [Fact]
    public void Open_FreshDatabase_SucceedsAndCreatesSchema()
    {
        var pathProvider = new TempPathProvider();
        using var sut = new VaultDatabaseService(pathProvider, new VaultMigrationService());

        try
        {
            SQLite.SQLiteConnection connection = sut.Open(ValidKey());

            Assert.True(sut.IsOpen);
            Assert.True(File.Exists(pathProvider.GetDatabaseFilePath()));

            int tableCount = connection.ExecuteScalar<int>(
                "SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name = 'VaultMetadata';");
            Assert.Equal(1, tableCount);
        }
        finally
        {
            sut.Close();
            pathProvider.Cleanup();
        }
    }

    [Fact]
    public void Open_CalledTwice_ReturnsSameConnectionInstance()
    {
        var pathProvider = new TempPathProvider();
        using var sut = new VaultDatabaseService(pathProvider, new VaultMigrationService());

        try
        {
            SQLite.SQLiteConnection first = sut.Open(ValidKey());
            SQLite.SQLiteConnection second = sut.Open(ValidKey());

            Assert.Same(first, second);
        }
        finally
        {
            sut.Close();
            pathProvider.Cleanup();
        }
    }

    [Fact]
    public void Open_ReopenedAfterClose_RecreatesSchemaSuccessfully()
    {
        // Simulates the app locking (closing the connection) and later unlocking again --
        // schema creation must be idempotent across separate Open() calls on separate
        // connections to the same underlying file, not just within one open connection.
        var pathProvider = new TempPathProvider();

        try
        {
            using (var first = new VaultDatabaseService(pathProvider, new VaultMigrationService()))
            {
                first.Open(ValidKey());
            }

            using var second = new VaultDatabaseService(pathProvider, new VaultMigrationService());
            Exception? exception = Record.Exception(() => second.Open(ValidKey()));

            Assert.Null(exception);
            Assert.True(second.IsOpen);
        }
        finally
        {
            pathProvider.Cleanup();
        }
    }

    [Theory]
    [InlineData(16)]
    [InlineData(24)]
    [InlineData(0)]
    public void Open_WrongKeyLength_ThrowsArgumentException(int wrongLength)
    {
        var pathProvider = new TempPathProvider();
        using var sut = new VaultDatabaseService(pathProvider, new VaultMigrationService());

        Assert.Throws<ArgumentException>(() => sut.Open(new byte[wrongLength]));
    }

    [Fact]
    public void Open_FileIsNotAValidDatabase_ThrowsVaultDatabaseException()
    {
        // This is the same failure signature SQLCipher produces for a wrong key: the bytes
        // don't parse as a valid (decrypted) SQLite database. Genuinely exercises
        // VaultDatabaseService's error-translation path without requiring the SQLCipher
        // native library, which isn't available on this desktop test runner (see
        // ARCHITECTURE.md for why, and how to validate real key-rejection on-device).
        var pathProvider = new TempPathProvider();
        System.IO.Directory.CreateDirectory(pathProvider.Directory);
        File.WriteAllBytes(pathProvider.GetDatabaseFilePath(), [1, 2, 3, 4, 5, 6, 7, 8]);

        using var sut = new VaultDatabaseService(pathProvider, new VaultMigrationService());

        try
        {
            Assert.Throws<VaultDatabaseException>(() => sut.Open(ValidKey()));
            Assert.False(sut.IsOpen);
        }
        finally
        {
            pathProvider.Cleanup();
        }
    }

    [Fact]
    public void Open_SchemaVersionNewerThanSupported_ThrowsVaultDatabaseException()
    {
        var pathProvider = new TempPathProvider();
        System.IO.Directory.CreateDirectory(pathProvider.Directory);

        // Pre-seed a VaultMetadata row claiming a future schema version, simulating a vault
        // created by a newer app version.
        using (var seed = new SQLite.SQLiteConnection(pathProvider.GetDatabaseFilePath()))
        {
            VaultSchema.EnsureCreated(seed);
            seed.Execute("INSERT INTO VaultMetadata (Id, SchemaVersion, CreatedAtUtc) VALUES (1, 999, 0);");
        }

        using var sut = new VaultDatabaseService(pathProvider, new VaultMigrationService());

        try
        {
            Assert.Throws<VaultDatabaseException>(() => sut.Open(ValidKey()));
        }
        finally
        {
            pathProvider.Cleanup();
        }
    }
}
