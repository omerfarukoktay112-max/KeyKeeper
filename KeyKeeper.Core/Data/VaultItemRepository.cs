using System.Text.Json;
using KeyKeeper.Core.Auth;
using KeyKeeper.Core.Crypto;
using KeyKeeper.Core.Models;
using SQLite;

namespace KeyKeeper.Core.Data;

/// <summary>
/// <see cref="IVaultItemRepository"/> backed by the <c>VaultItems</c>/<c>Tags</c>/<c>ItemTags</c>
/// tables (see <see cref="VaultSchema"/>) and <see cref="IAuthenticationService"/>'s
/// <c>EncryptWithVaultKey</c>/<c>DecryptWithVaultKey</c> for the per-item AES-GCM envelope.
///
/// Memory hygiene, the balance actually achievable here: a payload record's own sensitive
/// fields (a <see cref="Models.LoginItem"/>'s <c>Password</c>, say) are ordinary immutable
/// <see cref="string"/>s -- nothing in .NET can reliably zero a <see cref="string"/>'s backing
/// memory, and that's not attempted here (see <see cref="IVaultItemPayload"/>'s doc comment).
/// What <em>is</em> fully owned and zeroed, in a <c>finally</c> block, immediately after use:
/// - <see cref="EncryptPayload{TPayload}"/> serializes straight to a <c>byte[]</c> via
///   <see cref="JsonSerializer.SerializeToUtf8Bytes{TValue}(TValue, JsonSerializerOptions?)"/>
///   (never materializing a giant intermediate JSON <see cref="string"/> at all, not just
///   zeroing one after the fact), passes that buffer to <c>EncryptWithVaultKey</c>, then clears
///   it -- so the one buffer holding the *entire* plaintext payload (every sensitive field, not
///   just one) exists only as long as it takes to encrypt it.
/// - <see cref="DecryptPayload"/> does the mirror image: the raw decrypted JSON <c>byte[]</c>
///   from <c>DecryptWithVaultKey</c> is deserialized into the typed record, then cleared.
/// </summary>
public sealed class VaultItemRepository : IVaultItemRepository
{
    private readonly IVaultDatabaseService _vaultDatabase;
    private readonly IAuthenticationService _auth;

    public VaultItemRepository(IVaultDatabaseService vaultDatabase, IAuthenticationService auth)
    {
        _vaultDatabase = vaultDatabase ?? throw new ArgumentNullException(nameof(vaultDatabase));
        _auth = auth ?? throw new ArgumentNullException(nameof(auth));
    }

    public VaultItemRecord Create<TPayload>(TPayload payload, string? folderId = null)
        where TPayload : IVaultItemPayload
    {
        ArgumentNullException.ThrowIfNull(payload);

        string id = Guid.NewGuid().ToString();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        byte[] encrypted = EncryptPayload(payload);

        SQLiteConnection connection = _vaultDatabase.GetOpenConnectionOrThrow();
        connection.Execute(
            """
            INSERT INTO VaultItems (Id, ItemType, FolderId, IsFavorite, CreatedAtUtc, UpdatedAtUtc, EncryptedPayload)
            VALUES (?, ?, ?, 0, ?, ?, ?);
            """,
            id, (int)TPayload.ItemType, folderId, now.UtcTicks, now.UtcTicks, encrypted);

        return new VaultItemRecord(id, TPayload.ItemType, folderId, false, now, now, [], payload);
    }

    public VaultItemRecord? Get(string id)
    {
        ArgumentNullException.ThrowIfNull(id);

        SQLiteConnection connection = _vaultDatabase.GetOpenConnectionOrThrow();
        ItemRow? row = connection.Query<ItemRow>("SELECT * FROM VaultItems WHERE Id = ?;", id).FirstOrDefault();
        return row is null ? null : ToRecord(connection, row);
    }

    public IReadOnlyList<VaultItemRecord> List(VaultItemType? type = null)
    {
        SQLiteConnection connection = _vaultDatabase.GetOpenConnectionOrThrow();
        List<ItemRow> rows = type is { } t
            ? connection.Query<ItemRow>("SELECT * FROM VaultItems WHERE ItemType = ? ORDER BY UpdatedAtUtc DESC;", (int)t)
            : connection.Query<ItemRow>("SELECT * FROM VaultItems ORDER BY UpdatedAtUtc DESC;");

        return rows.Select(row => ToRecord(connection, row)).ToList();
    }

    public VaultItemRecord Update<TPayload>(string id, TPayload payload)
        where TPayload : IVaultItemPayload
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(payload);

        SQLiteConnection connection = _vaultDatabase.GetOpenConnectionOrThrow();
        ItemRow? existing = connection.Query<ItemRow>("SELECT * FROM VaultItems WHERE Id = ?;", id).FirstOrDefault();
        if (existing is null)
        {
            throw new KeyNotFoundException($"No vault item with id '{id}' exists.");
        }

        var existingType = (VaultItemType)existing.ItemType;
        if (existingType != TPayload.ItemType)
        {
            throw new ArgumentException(
                $"Item '{id}' is a {existingType} item; cannot update it with a {TPayload.ItemType} payload.",
                nameof(payload));
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        byte[] encrypted = EncryptPayload(payload);
        connection.Execute("UPDATE VaultItems SET EncryptedPayload = ?, UpdatedAtUtc = ? WHERE Id = ?;", encrypted, now.UtcTicks, id);

        IReadOnlyList<string> tags = GetTags(connection, id);
        return new VaultItemRecord(
            id,
            TPayload.ItemType,
            existing.FolderId,
            existing.IsFavorite,
            new DateTimeOffset(existing.CreatedAtUtc, TimeSpan.Zero),
            now,
            tags,
            payload);
    }

    public void Delete(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        _vaultDatabase.GetOpenConnectionOrThrow().Execute("DELETE FROM VaultItems WHERE Id = ?;", id);
    }

    public void SetFavorite(string id, bool isFavorite)
    {
        ArgumentNullException.ThrowIfNull(id);
        _vaultDatabase.GetOpenConnectionOrThrow().Execute(
            "UPDATE VaultItems SET IsFavorite = ? WHERE Id = ?;", isFavorite, id);
    }

    public void SetFolder(string id, string? folderId)
    {
        ArgumentNullException.ThrowIfNull(id);
        _vaultDatabase.GetOpenConnectionOrThrow().Execute(
            "UPDATE VaultItems SET FolderId = ? WHERE Id = ?;", folderId, id);
    }

    public void AddTag(string id, string tagName)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(tagName);

        SQLiteConnection connection = _vaultDatabase.GetOpenConnectionOrThrow();
        string tagId = GetOrCreateTagId(connection, tagName);
        connection.Execute("INSERT OR IGNORE INTO ItemTags (ItemId, TagId) VALUES (?, ?);", id, tagId);
    }

    public void RemoveTag(string id, string tagName)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(tagName);

        SQLiteConnection connection = _vaultDatabase.GetOpenConnectionOrThrow();
        string? tagId = connection.ExecuteScalar<string>("SELECT Id FROM Tags WHERE Name = ?;", tagName);
        if (tagId is not null)
        {
            connection.Execute("DELETE FROM ItemTags WHERE ItemId = ? AND TagId = ?;", id, tagId);
        }
    }

    private byte[] EncryptPayload<TPayload>(TPayload payload)
    {
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(payload);
        try
        {
            return _auth.EncryptWithVaultKey(json);
        }
        finally
        {
            SecureMemoryHelper.Clear(json);
        }
    }

    private object DecryptPayload(VaultItemType type, byte[] encryptedPayload)
    {
        byte[] json = _auth.DecryptWithVaultKey(encryptedPayload);
        try
        {
            return DeserializePayload(type, json);
        }
        finally
        {
            SecureMemoryHelper.Clear(json);
        }
    }

    private static object DeserializePayload(VaultItemType type, byte[] json) => type switch
    {
        VaultItemType.Login => (object?)JsonSerializer.Deserialize<LoginItem>(json)
            ?? throw new VaultDatabaseException("Stored Login item payload deserialized to null."),
        VaultItemType.Card => (object?)JsonSerializer.Deserialize<CardItem>(json)
            ?? throw new VaultDatabaseException("Stored Card item payload deserialized to null."),
        VaultItemType.WiFi => (object?)JsonSerializer.Deserialize<WifiItem>(json)
            ?? throw new VaultDatabaseException("Stored WiFi item payload deserialized to null."),
        VaultItemType.Note => (object?)JsonSerializer.Deserialize<SecureNoteItem>(json)
            ?? throw new VaultDatabaseException("Stored Note item payload deserialized to null."),
        VaultItemType.License => (object?)JsonSerializer.Deserialize<LicenseItem>(json)
            ?? throw new VaultDatabaseException("Stored License item payload deserialized to null."),
        _ => throw new VaultDatabaseException($"Unknown VaultItemType value '{(int)type}'."),
    };

    private VaultItemRecord ToRecord(SQLiteConnection connection, ItemRow row)
    {
        var type = (VaultItemType)row.ItemType;
        object payload = DecryptPayload(type, row.EncryptedPayload);
        IReadOnlyList<string> tags = GetTags(connection, row.Id);

        return new VaultItemRecord(
            row.Id,
            type,
            row.FolderId,
            row.IsFavorite,
            new DateTimeOffset(row.CreatedAtUtc, TimeSpan.Zero),
            new DateTimeOffset(row.UpdatedAtUtc, TimeSpan.Zero),
            tags,
            payload);
    }

    private static string GetOrCreateTagId(SQLiteConnection connection, string tagName)
    {
        string? existingId = connection.ExecuteScalar<string>("SELECT Id FROM Tags WHERE Name = ?;", tagName);
        if (existingId is not null)
        {
            return existingId;
        }

        string newId = Guid.NewGuid().ToString();
        connection.Execute("INSERT INTO Tags (Id, Name) VALUES (?, ?);", newId, tagName);
        return newId;
    }

    private static IReadOnlyList<string> GetTags(SQLiteConnection connection, string itemId) =>
        connection.Query<TagNameRow>(
                """
                SELECT t.Name AS Name FROM Tags t
                JOIN ItemTags it ON it.TagId = t.Id
                WHERE it.ItemId = ?
                ORDER BY t.Name;
                """,
                itemId)
            .Select(r => r.Name)
            .ToList();

    private sealed class ItemRow
    {
        public string Id { get; set; } = string.Empty;
        public int ItemType { get; set; }
        public string? FolderId { get; set; }
        public bool IsFavorite { get; set; }
        public long CreatedAtUtc { get; set; }
        public long UpdatedAtUtc { get; set; }
        public byte[] EncryptedPayload { get; set; } = [];
    }

    private sealed class TagNameRow
    {
        public string Name { get; set; } = string.Empty;
    }
}
