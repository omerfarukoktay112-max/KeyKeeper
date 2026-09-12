using KeyKeeper.Core.Auth;
using KeyKeeper.Core.Crypto;
using KeyKeeper.Core.Data;
using KeyKeeper.Core.Models;

namespace KeyKeeper.Core.Tests.Data;

[Collection(nameof(VaultDatabaseTestCollection))]
public class VaultItemRepositoryTests
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

    /// <summary>
    /// Everything except <c>Encrypt/DecryptWithVaultKey</c> throws -- the repository never calls
    /// anything else on <see cref="IAuthenticationService"/>, so these tests exercise the real
    /// encryption round trip (via a real <see cref="AesGcmEncryptionService"/> and a fixed test
    /// key) without needing a real unlock flow, matching this codebase's "fake the out-of-scope
    /// dependency, use the real cheap component" style (see <c>WrappedKeyStorageTests</c>).
    /// </summary>
    private class FakeVaultKeyAuthenticationService : IAuthenticationService
    {
        private static readonly byte[] Key = [.. Enumerable.Range(0, 32).Select(i => (byte)i)];
        private readonly IEncryptionService _encryption = new AesGcmEncryptionService();

        public bool IsVaultInitialized => true;
        public bool IsUnlocked { get; set; } = true;
#pragma warning disable CS0067 // required by the interface; the repository never subscribes to it
        public event Action? Locked;
#pragma warning restore CS0067
        public TimeSpan AutoLockTimeout { get; set; }

        public Task<AuthenticationResult> InitializeVaultAsync(char[] masterPassword) => throw new NotSupportedException();
        public Task<AuthenticationResult> UnlockWithMasterPasswordAsync(char[] masterPassword) => throw new NotSupportedException();
        public Task<AuthenticationResult> TryUnlockWithBiometricsAsync() => throw new NotSupportedException();
        public Task EnableBiometricUnlockAsync() => throw new NotSupportedException();
        public void Lock() => IsUnlocked = false;
        public void NotifyUserActivity() { }

        public virtual byte[] EncryptWithVaultKey(byte[] plaintext)
        {
            if (!IsUnlocked)
            {
                throw new InvalidOperationException("The vault is locked.");
            }

            return _encryption.Encrypt(plaintext, Key);
        }

        public virtual byte[] DecryptWithVaultKey(byte[] ciphertext)
        {
            if (!IsUnlocked)
            {
                throw new InvalidOperationException("The vault is locked.");
            }

            return _encryption.Decrypt(ciphertext, Key);
        }
    }

    /// <summary>Captures the exact buffer references the repository hands to/receives from
    /// encryption, so tests can assert on <em>the repository's own hygiene</em> -- that it
    /// actually zeroes the buffer it owns -- rather than just that some `Clear` call happened
    /// somewhere.</summary>
    private sealed class SpyAuthenticationService : FakeVaultKeyAuthenticationService
    {
        public byte[]? LastEncryptedPlaintext { get; private set; }
        public byte[]? LastDecryptedPlaintext { get; private set; }

        public override byte[] EncryptWithVaultKey(byte[] plaintext)
        {
            LastEncryptedPlaintext = plaintext;
            return base.EncryptWithVaultKey(plaintext);
        }

        public override byte[] DecryptWithVaultKey(byte[] ciphertext)
        {
            byte[] plaintext = base.DecryptWithVaultKey(ciphertext);
            LastDecryptedPlaintext = plaintext;
            return plaintext;
        }
    }

    private sealed class Sut : IDisposable
    {
        public required VaultItemRepository Repository { get; init; }
        public required IVaultDatabaseService Database { get; init; }
        public required TempPathProvider PathProvider { get; init; }

        public void Dispose()
        {
            Database.Dispose();
            PathProvider.Cleanup();
        }
    }

    private static Sut CreateSut(IAuthenticationService? auth = null)
    {
        var pathProvider = new TempPathProvider();
        IVaultDatabaseService database = new VaultDatabaseService(pathProvider, new VaultMigrationService());
        database.Open(new byte[VaultDatabaseService.RequiredKeyLengthBytes]);
        var repository = new VaultItemRepository(database, auth ?? new FakeVaultKeyAuthenticationService());

        return new Sut { Repository = repository, Database = database, PathProvider = pathProvider };
    }

    [Fact]
    public void Create_ThenGet_RoundTripsALoginItem()
    {
        using Sut sut = CreateSut();
        var login = new LoginItem("GitHub", "octocat", "hunter2", "https://github.com", "JBSWY3DPEHPK3PXP");

        VaultItemRecord created = sut.Repository.Create(login);
        VaultItemRecord? fetched = sut.Repository.Get(created.Id);

        Assert.NotNull(fetched);
        Assert.Equal(VaultItemType.Login, fetched!.ItemType);
        Assert.Equal(login, fetched.Payload);
    }

    [Fact]
    public void Create_ThenGet_RoundTripsACardItem()
    {
        using Sut sut = CreateSut();
        var card = new CardItem("Personal Visa", "Ada Lovelace", "4111111111111111", 12, 2030, "123", CardType.Visa);

        VaultItemRecord created = sut.Repository.Create(card);
        VaultItemRecord? fetched = sut.Repository.Get(created.Id);

        Assert.NotNull(fetched);
        Assert.Equal(VaultItemType.Card, fetched!.ItemType);
        Assert.Equal(card, fetched.Payload);
    }

    [Fact]
    public void Create_ThenGet_RoundTripsAWifiItem()
    {
        using Sut sut = CreateSut();
        var wifi = new WifiItem("Home", "MyNetwork", "correcthorsebatterystaple", WifiSecurityProtocol.Wpa3, IsHidden: false);

        VaultItemRecord created = sut.Repository.Create(wifi);
        VaultItemRecord? fetched = sut.Repository.Get(created.Id);

        Assert.NotNull(fetched);
        Assert.Equal(VaultItemType.WiFi, fetched!.ItemType);
        Assert.Equal(wifi, fetched.Payload);
    }

    [Fact]
    public void Create_ThenGet_RoundTripsASecureNoteItem()
    {
        using Sut sut = CreateSut();
        var note = new SecureNoteItem("Passport", "Passport number: X1234567");

        VaultItemRecord created = sut.Repository.Create(note);
        VaultItemRecord? fetched = sut.Repository.Get(created.Id);

        Assert.NotNull(fetched);
        Assert.Equal(VaultItemType.Note, fetched!.ItemType);
        Assert.Equal(note, fetched.Payload);
    }

    [Fact]
    public void Create_ThenGet_RoundTripsALicenseItem()
    {
        using Sut sut = CreateSut();
        var license = new LicenseItem("Office 365", "AAAAA-BBBBB-CCCCC-DDDDD", "Microsoft Office", DateTimeOffset.Parse("2027-01-01T00:00:00Z"));

        VaultItemRecord created = sut.Repository.Create(license);
        VaultItemRecord? fetched = sut.Repository.Get(created.Id);

        Assert.NotNull(fetched);
        Assert.Equal(VaultItemType.License, fetched!.ItemType);
        Assert.Equal(license, fetched.Payload);
    }

    [Fact]
    public void Get_NoSuchId_ReturnsNull()
    {
        using Sut sut = CreateSut();

        Assert.Null(sut.Repository.Get(Guid.NewGuid().ToString()));
    }

    [Fact]
    public void Create_VaultIsLocked_PropagatesInvalidOperationException()
    {
        var auth = new FakeVaultKeyAuthenticationService { IsUnlocked = false };
        using Sut sut = CreateSut(auth);

        Assert.Throws<InvalidOperationException>(() => sut.Repository.Create(new SecureNoteItem("Title", "Content")));
    }

    [Fact]
    public void List_NoTypeFilter_ReturnsEveryItem()
    {
        using Sut sut = CreateSut();
        sut.Repository.Create(new SecureNoteItem("Note", "Content"));
        sut.Repository.Create(new WifiItem("WiFi", "SSID", "pw", WifiSecurityProtocol.Wpa2, false));

        IReadOnlyList<VaultItemRecord> items = sut.Repository.List();

        Assert.Equal(2, items.Count);
    }

    [Fact]
    public void List_WithTypeFilter_ReturnsOnlyThatType()
    {
        using Sut sut = CreateSut();
        sut.Repository.Create(new SecureNoteItem("Note", "Content"));
        VaultItemRecord wifi = sut.Repository.Create(new WifiItem("WiFi", "SSID", "pw", WifiSecurityProtocol.Wpa2, false));

        IReadOnlyList<VaultItemRecord> items = sut.Repository.List(VaultItemType.WiFi);

        VaultItemRecord onlyItem = Assert.Single(items);
        Assert.Equal(wifi.Id, onlyItem.Id);
    }

    [Fact]
    public void ListByFolder_ReturnsOnlyItemsDirectlyInThatFolder()
    {
        using Sut sut = CreateSut();
        string folderId = CreateFolder(sut, "Work");
        VaultItemRecord inFolder = sut.Repository.Create(new SecureNoteItem("In folder", "Content"), folderId: folderId);
        sut.Repository.Create(new SecureNoteItem("Unfiled", "Content"));

        IReadOnlyList<VaultItemRecord> items = sut.Repository.ListByFolder(folderId);

        VaultItemRecord onlyItem = Assert.Single(items);
        Assert.Equal(inFolder.Id, onlyItem.Id);
    }

    [Fact]
    public void ListByFolder_Null_ReturnsOnlyUnfiledItems()
    {
        using Sut sut = CreateSut();
        string folderId = CreateFolder(sut, "Work");
        sut.Repository.Create(new SecureNoteItem("In folder", "Content"), folderId: folderId);
        VaultItemRecord unfiled = sut.Repository.Create(new SecureNoteItem("Unfiled", "Content"));

        IReadOnlyList<VaultItemRecord> items = sut.Repository.ListByFolder(null);

        VaultItemRecord onlyItem = Assert.Single(items);
        Assert.Equal(unfiled.Id, onlyItem.Id);
    }

    [Fact]
    public void ListByTag_ReturnsOnlyItemsTaggedWithIt()
    {
        using Sut sut = CreateSut();
        VaultItemRecord tagged = sut.Repository.Create(new SecureNoteItem("Tagged", "Content"));
        sut.Repository.Create(new SecureNoteItem("Untagged", "Content"));
        sut.Repository.AddTag(tagged.Id, "Work");
        string tagId = sut.Database.GetOpenConnectionOrThrow()
            .ExecuteScalar<string>("SELECT Id FROM Tags WHERE Name = ?;", "Work");

        IReadOnlyList<VaultItemRecord> items = sut.Repository.ListByTag(tagId);

        VaultItemRecord onlyItem = Assert.Single(items);
        Assert.Equal(tagged.Id, onlyItem.Id);
    }

    [Fact]
    public void ListFavorites_ReturnsOnlyFavoritedItems()
    {
        using Sut sut = CreateSut();
        VaultItemRecord favorite = sut.Repository.Create(new SecureNoteItem("Favorite", "Content"));
        sut.Repository.SetFavorite(favorite.Id, true);
        sut.Repository.Create(new SecureNoteItem("Not favorite", "Content"));

        IReadOnlyList<VaultItemRecord> items = sut.Repository.ListFavorites();

        VaultItemRecord onlyItem = Assert.Single(items);
        Assert.Equal(favorite.Id, onlyItem.Id);
    }

    [Fact]
    public void Update_ReplacesThePayloadAndBumpsUpdatedAtUtc()
    {
        using Sut sut = CreateSut();
        VaultItemRecord created = sut.Repository.Create(new SecureNoteItem("Note", "Original"));
        var updatedPayload = new SecureNoteItem("Note", "Changed");

        VaultItemRecord updated = sut.Repository.Update(created.Id, updatedPayload);
        VaultItemRecord? fetched = sut.Repository.Get(created.Id);

        Assert.Equal(updatedPayload, updated.Payload);
        Assert.Equal(updatedPayload, fetched!.Payload);
        Assert.True(updated.UpdatedAtUtc >= created.UpdatedAtUtc);
        Assert.Equal(created.CreatedAtUtc, updated.CreatedAtUtc);
    }

    [Fact]
    public void Update_NoSuchId_ThrowsKeyNotFoundException()
    {
        using Sut sut = CreateSut();

        Assert.Throws<KeyNotFoundException>(
            () => sut.Repository.Update(Guid.NewGuid().ToString(), new SecureNoteItem("Note", "Content")));
    }

    [Fact]
    public void Update_PayloadTypeDoesNotMatchStoredItemType_ThrowsArgumentException()
    {
        using Sut sut = CreateSut();
        VaultItemRecord note = sut.Repository.Create(new SecureNoteItem("Note", "Content"));

        Assert.Throws<ArgumentException>(
            () => sut.Repository.Update(note.Id, new WifiItem("WiFi", "SSID", "pw", WifiSecurityProtocol.Open, false)));
    }

    [Fact]
    public void Delete_RemovesTheItem()
    {
        using Sut sut = CreateSut();
        VaultItemRecord created = sut.Repository.Create(new SecureNoteItem("Note", "Content"));

        sut.Repository.Delete(created.Id);

        Assert.Null(sut.Repository.Get(created.Id));
    }

    [Fact]
    public void Delete_NoSuchId_DoesNotThrow()
    {
        using Sut sut = CreateSut();

        Exception? exception = Record.Exception(() => sut.Repository.Delete(Guid.NewGuid().ToString()));

        Assert.Null(exception);
    }

    [Fact]
    public void SetFavorite_IsReflectedOnGet()
    {
        using Sut sut = CreateSut();
        VaultItemRecord created = sut.Repository.Create(new SecureNoteItem("Note", "Content"));
        Assert.False(created.IsFavorite);

        sut.Repository.SetFavorite(created.Id, true);

        Assert.True(sut.Repository.Get(created.Id)!.IsFavorite);
    }

    [Fact]
    public void SetFolder_IsReflectedOnGet()
    {
        using Sut sut = CreateSut();
        string folderId = CreateFolder(sut, "Test Folder");
        VaultItemRecord created = sut.Repository.Create(new SecureNoteItem("Note", "Content"));

        sut.Repository.SetFolder(created.Id, folderId);

        Assert.Equal(folderId, sut.Repository.Get(created.Id)!.FolderId);
    }

    [Fact]
    public void SetFolder_Null_ClearsTheFolder()
    {
        using Sut sut = CreateSut();
        string folderId = CreateFolder(sut, "Test Folder");
        VaultItemRecord created = sut.Repository.Create(new SecureNoteItem("Note", "Content"), folderId: folderId);

        sut.Repository.SetFolder(created.Id, null);

        Assert.Null(sut.Repository.Get(created.Id)!.FolderId);
    }

    /// <summary>Tests here only need a real <c>Folders</c> row to satisfy
    /// <c>VaultItems.FolderId</c>'s foreign key -- <see cref="FolderRepository"/> (added
    /// Phase 5) is the real way to create one; its own CRUD/nesting/cascade-delete behavior has
    /// its own test file, <c>FolderRepositoryTests</c>.</summary>
    private static string CreateFolder(Sut sut, string name) =>
        new FolderRepository(sut.Database).Create(name).Id;

    [Fact]
    public void AddTag_ThenGet_IncludesTheTag()
    {
        using Sut sut = CreateSut();
        VaultItemRecord created = sut.Repository.Create(new SecureNoteItem("Note", "Content"));

        sut.Repository.AddTag(created.Id, "important");

        Assert.Equal(["important"], sut.Repository.Get(created.Id)!.Tags);
    }

    [Fact]
    public void AddTag_CalledTwiceWithTheSameTag_DoesNotDuplicateIt()
    {
        using Sut sut = CreateSut();
        VaultItemRecord created = sut.Repository.Create(new SecureNoteItem("Note", "Content"));

        sut.Repository.AddTag(created.Id, "important");
        sut.Repository.AddTag(created.Id, "important");

        Assert.Equal(["important"], sut.Repository.Get(created.Id)!.Tags);
    }

    [Fact]
    public void AddTag_TwoItemsWithTheSameTagName_ShareOneTagRow()
    {
        using Sut sut = CreateSut();
        VaultItemRecord first = sut.Repository.Create(new SecureNoteItem("Note 1", "Content"));
        VaultItemRecord second = sut.Repository.Create(new SecureNoteItem("Note 2", "Content"));

        sut.Repository.AddTag(first.Id, "shared");
        sut.Repository.AddTag(second.Id, "shared");

        Assert.Equal(["shared"], sut.Repository.Get(first.Id)!.Tags);
        Assert.Equal(["shared"], sut.Repository.Get(second.Id)!.Tags);
    }

    [Fact]
    public void RemoveTag_RemovesItFromTheItem()
    {
        using Sut sut = CreateSut();
        VaultItemRecord created = sut.Repository.Create(new SecureNoteItem("Note", "Content"));
        sut.Repository.AddTag(created.Id, "important");

        sut.Repository.RemoveTag(created.Id, "important");

        Assert.Empty(sut.Repository.Get(created.Id)!.Tags);
    }

    [Fact]
    public void RemoveTag_NotPresent_DoesNotThrow()
    {
        using Sut sut = CreateSut();
        VaultItemRecord created = sut.Repository.Create(new SecureNoteItem("Note", "Content"));

        Exception? exception = Record.Exception(() => sut.Repository.RemoveTag(created.Id, "nope"));

        Assert.Null(exception);
    }

    [Fact]
    public void Create_ClearsThePlaintextPayloadBufferAfterEncrypting()
    {
        var spyAuth = new SpyAuthenticationService();
        using Sut sut = CreateSut(spyAuth);

        sut.Repository.Create(new SecureNoteItem("Note", "very secret content"));

        Assert.NotNull(spyAuth.LastEncryptedPlaintext);
        Assert.All(spyAuth.LastEncryptedPlaintext!, b => Assert.Equal(0, b));
    }

    [Fact]
    public void Get_ClearsTheDecryptedPayloadBufferAfterDeserializing()
    {
        var spyAuth = new SpyAuthenticationService();
        using Sut sut = CreateSut(spyAuth);
        VaultItemRecord created = sut.Repository.Create(new SecureNoteItem("Note", "very secret content"));

        sut.Repository.Get(created.Id);

        Assert.NotNull(spyAuth.LastDecryptedPlaintext);
        Assert.All(spyAuth.LastDecryptedPlaintext!, b => Assert.Equal(0, b));
    }
}
