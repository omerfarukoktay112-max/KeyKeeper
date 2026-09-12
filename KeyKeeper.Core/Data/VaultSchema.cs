using SQLite;

namespace KeyKeeper.Core.Data;

/// <summary>
/// Vault database schema (Phase 2): table structure only, no typed item models or CRUD yet —
/// those arrive in a later phase. All statements are idempotent
/// (<c>CREATE TABLE/INDEX IF NOT EXISTS</c>), so <see cref="EnsureCreated"/> is safe to call
/// every time the app opens the vault, not just on first run.
///
/// Design notes:
/// - <c>VaultMetadata</c> holds only <c>SchemaVersion</c> and <c>CreatedAtUtc</c> — NOT the
///   Argon2 salt/parameters or the master-password verifier. Those moved to the separate,
///   unencrypted <see cref="VaultUnlockStore"/> in Phase 3 (see its doc comment for why: this
///   table lives inside the SQLCipher-encrypted database, but the salt is needed to *derive*
///   the very key required to open that database in the first place — an unavoidable
///   chicken-and-egg problem that only resolves by keeping the salt/verifier outside it).
/// - <c>VaultItems.EncryptedPayload</c> is a single opaque BLOB (AES-GCM output from
///   <c>KeyKeeper.Core.Crypto.IEncryptionService</c>, wrapping type-specific fields as JSON).
///   Deliberately not modeled as separate columns per item type — every new item type or
///   field would otherwise require a schema migration; instead only <c>ItemType</c>
///   (see <c>KeyKeeper.Core.Models.VaultItemType</c>) is a plain column, for filtering/sorting.
/// - <c>Folders.Name</c> and <c>Tags.Name</c> are plain (unencrypted) columns, not
///   app-layer-encrypted like item payloads. This is intentional, not an oversight: the whole
///   database file is already encrypted at rest by SQLCipher (see <c>VaultDatabaseService</c>),
///   so an attacker without the database key can't read any table, folder/tag names included.
///   The extra app-layer AES-GCM envelope on item payloads exists for a different reason —
///   generic, per-type-schema-free storage — not because SQLCipher's page-level encryption is
///   insufficient for names.
/// - IDs are TEXT (GUID strings) rather than autoincrementing integers, since vault items are
///   generated client-side, may reference each other before any wider sync/export scheme
///   exists, and TEXT primary keys sidestep autoincrement-across-tables coordination.
/// </summary>
public static class VaultSchema
{
    private static readonly string[] Statements =
    [
        """
        CREATE TABLE IF NOT EXISTS VaultMetadata (
            Id INTEGER PRIMARY KEY CHECK (Id = 1),
            SchemaVersion INTEGER NOT NULL,
            CreatedAtUtc INTEGER NOT NULL
        );
        """,

        """
        CREATE TABLE IF NOT EXISTS Folders (
            Id TEXT PRIMARY KEY NOT NULL,
            Name TEXT NOT NULL,
            ParentFolderId TEXT NULL,
            FOREIGN KEY (ParentFolderId) REFERENCES Folders (Id) ON DELETE CASCADE
        );
        """,
        "CREATE INDEX IF NOT EXISTS IX_Folders_ParentFolderId ON Folders (ParentFolderId);",

        """
        CREATE TABLE IF NOT EXISTS VaultItems (
            Id TEXT PRIMARY KEY NOT NULL,
            ItemType INTEGER NOT NULL,
            FolderId TEXT NULL,
            IsFavorite INTEGER NOT NULL DEFAULT 0,
            CreatedAtUtc INTEGER NOT NULL,
            UpdatedAtUtc INTEGER NOT NULL,
            EncryptedPayload BLOB NOT NULL,
            FOREIGN KEY (FolderId) REFERENCES Folders (Id) ON DELETE SET NULL
        );
        """,
        "CREATE INDEX IF NOT EXISTS IX_VaultItems_FolderId ON VaultItems (FolderId);",
        "CREATE INDEX IF NOT EXISTS IX_VaultItems_ItemType ON VaultItems (ItemType);",

        """
        CREATE TABLE IF NOT EXISTS Tags (
            Id TEXT PRIMARY KEY NOT NULL,
            Name TEXT NOT NULL UNIQUE
        );
        """,

        """
        CREATE TABLE IF NOT EXISTS ItemTags (
            ItemId TEXT NOT NULL,
            TagId TEXT NOT NULL,
            PRIMARY KEY (ItemId, TagId),
            FOREIGN KEY (ItemId) REFERENCES VaultItems (Id) ON DELETE CASCADE,
            FOREIGN KEY (TagId) REFERENCES Tags (Id) ON DELETE CASCADE
        );
        """,
        "CREATE INDEX IF NOT EXISTS IX_ItemTags_TagId ON ItemTags (TagId);",
    ];

    public static void EnsureCreated(SQLiteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        foreach (string statement in Statements)
        {
            connection.Execute(statement);
        }
    }
}
