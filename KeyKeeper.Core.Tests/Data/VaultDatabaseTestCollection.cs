using SQLitePCL;

namespace KeyKeeper.Core.Tests.Data;

/// <summary>
/// Forces the plain (non-cipher) e_sqlite3 native provider for every test in this collection.
/// SQLCipher's native binary (SQLitePCLRaw.bundle_e_sqlcipher) is only published for
/// Android/iOS RIDs, not desktop -- so these tests exercise real SQLite behavior for schema
/// DDL, idempotency, and error-translation logic, but cannot exercise SQLCipher's actual
/// key-based encryption/rejection. See ARCHITECTURE.md for the full reasoning and what this
/// does and doesn't prove.
/// </summary>
public sealed class VaultDatabaseTestFixture
{
    public VaultDatabaseTestFixture()
    {
        raw.SetProvider(new SQLite3Provider_e_sqlite3());
    }
}

[CollectionDefinition(nameof(VaultDatabaseTestCollection))]
public sealed class VaultDatabaseTestCollection : ICollectionFixture<VaultDatabaseTestFixture>
{
}
