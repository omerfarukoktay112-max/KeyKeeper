using KeyKeeper.Core.Auth;
using KeyKeeper.Core.Crypto;
using KeyKeeper.Core.Data;
using KeyKeeper.Core.Models;

namespace KeyKeeper.Core.Tests.Data;

[Collection(nameof(VaultDatabaseTestCollection))]
public class FolderRepositoryTests
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

    /// <summary>Always-unlocked, real-encryption fake -- same style as
    /// <c>VaultItemRepositoryTests.FakeVaultKeyAuthenticationService</c> -- needed only by the
    /// tests here that also create a <see cref="VaultItemRepository"/> to prove folder deletion's
    /// effect on items.</summary>
    private sealed class FakeVaultKeyAuthenticationService : IAuthenticationService
    {
        private static readonly byte[] Key = [.. Enumerable.Range(0, 32).Select(i => (byte)i)];
        private readonly IEncryptionService _encryption = new AesGcmEncryptionService();

        public bool IsVaultInitialized => true;
        public bool IsUnlocked => true;
#pragma warning disable CS0067
        public event Action? Locked;
#pragma warning restore CS0067
        public TimeSpan AutoLockTimeout { get; set; }

        public Task<AuthenticationResult> InitializeVaultAsync(char[] masterPassword) => throw new NotSupportedException();
        public Task<AuthenticationResult> UnlockWithMasterPasswordAsync(char[] masterPassword) => throw new NotSupportedException();
        public Task<AuthenticationResult> TryUnlockWithBiometricsAsync() => throw new NotSupportedException();
        public Task EnableBiometricUnlockAsync() => throw new NotSupportedException();
        public void Lock() => throw new NotSupportedException();
        public void NotifyUserActivity() { }
        public byte[] EncryptWithVaultKey(byte[] plaintext) => _encryption.Encrypt(plaintext, Key);
        public byte[] DecryptWithVaultKey(byte[] ciphertext) => _encryption.Decrypt(ciphertext, Key);
    }

    private sealed class Sut : IDisposable
    {
        public required FolderRepository Folders { get; init; }
        public required VaultItemRepository Items { get; init; }
        public required TempPathProvider PathProvider { get; init; }
        public required IVaultDatabaseService Database { get; init; }

        public static Sut Create()
        {
            var pathProvider = new TempPathProvider();
            IVaultDatabaseService database = new VaultDatabaseService(pathProvider, new VaultMigrationService());
            database.Open(new byte[VaultDatabaseService.RequiredKeyLengthBytes]);

            return new Sut
            {
                Folders = new FolderRepository(database),
                Items = new VaultItemRepository(database, new FakeVaultKeyAuthenticationService()),
                PathProvider = pathProvider,
                Database = database,
            };
        }

        public void Dispose()
        {
            Database.Dispose();
            PathProvider.Cleanup();
        }
    }

    [Fact]
    public void Create_ThenGet_RoundTripsARootFolder()
    {
        using Sut sut = Sut.Create();

        FolderRecord created = sut.Folders.Create("Work");
        FolderRecord? fetched = sut.Folders.Get(created.Id);

        Assert.NotNull(fetched);
        Assert.Equal("Work", fetched!.Name);
        Assert.Null(fetched.ParentFolderId);
    }

    [Fact]
    public void Create_WithParent_SetsParentFolderId()
    {
        using Sut sut = Sut.Create();

        FolderRecord parent = sut.Folders.Create("Work");
        FolderRecord child = sut.Folders.Create("Clients", parent.Id);

        Assert.Equal(parent.Id, child.ParentFolderId);
    }

    [Fact]
    public void Create_WithNonExistentParent_ThrowsKeyNotFoundException()
    {
        using Sut sut = Sut.Create();

        Assert.Throws<KeyNotFoundException>(() => sut.Folders.Create("Orphan", "no-such-id"));
    }

    [Fact]
    public void Get_NoSuchId_ReturnsNull()
    {
        using Sut sut = Sut.Create();

        Assert.Null(sut.Folders.Get("no-such-id"));
    }

    [Fact]
    public void ListAll_ReturnsEveryFolderFlatRegardlessOfNesting()
    {
        using Sut sut = Sut.Create();
        FolderRecord parent = sut.Folders.Create("Work");
        sut.Folders.Create("Clients", parent.Id);
        sut.Folders.Create("Personal");

        IReadOnlyList<FolderRecord> all = sut.Folders.ListAll();

        Assert.Equal(3, all.Count);
    }

    [Fact]
    public void Rename_UpdatesTheName()
    {
        using Sut sut = Sut.Create();
        FolderRecord folder = sut.Folders.Create("Work");

        sut.Folders.Rename(folder.Id, "Work (Old)");

        Assert.Equal("Work (Old)", sut.Folders.Get(folder.Id)!.Name);
    }

    [Fact]
    public void Rename_NoSuchId_ThrowsKeyNotFoundException()
    {
        using Sut sut = Sut.Create();

        Assert.Throws<KeyNotFoundException>(() => sut.Folders.Rename("no-such-id", "New Name"));
    }

    [Fact]
    public void Delete_RemovesTheFolder()
    {
        using Sut sut = Sut.Create();
        FolderRecord folder = sut.Folders.Create("Work");

        sut.Folders.Delete(folder.Id);

        Assert.Null(sut.Folders.Get(folder.Id));
    }

    [Fact]
    public void Delete_NoSuchId_DoesNotThrow()
    {
        using Sut sut = Sut.Create();

        sut.Folders.Delete("no-such-id");
    }

    [Fact]
    public void Delete_CascadesToDescendantFolders()
    {
        using Sut sut = Sut.Create();
        FolderRecord parent = sut.Folders.Create("Work");
        FolderRecord child = sut.Folders.Create("Clients", parent.Id);
        FolderRecord grandchild = sut.Folders.Create("Acme Corp", child.Id);

        sut.Folders.Delete(parent.Id);

        Assert.Null(sut.Folders.Get(parent.Id));
        Assert.Null(sut.Folders.Get(child.Id));
        Assert.Null(sut.Folders.Get(grandchild.Id));
    }

    /// <summary>
    /// The decision this repository encodes (see <see cref="IFolderRepository.Delete"/>'s doc
    /// comment): deleting a folder never destroys or blocks on the items inside it -- they become
    /// unfiled instead. This is enforced by the schema's own <c>ON DELETE SET NULL</c> foreign
    /// key, not application code, but is exactly the behavior a regression could silently break
    /// (e.g. a future migration accidentally changing the FK action), so it's tested directly.
    /// </summary>
    [Fact]
    public void Delete_OrphansItemsDirectlyInsideIt_RatherThanDeletingOrBlocking()
    {
        using Sut sut = Sut.Create();
        FolderRecord folder = sut.Folders.Create("Work");
        VaultItemRecord item = sut.Items.Create(new LoginItem("Work Email", "me", "pw", null, null), folder.Id);

        sut.Folders.Delete(folder.Id);

        VaultItemRecord? afterDelete = sut.Items.Get(item.Id);
        Assert.NotNull(afterDelete);
        Assert.Null(afterDelete!.FolderId);
    }

    [Fact]
    public void Delete_OrphansItemsInsideCascadeDeletedDescendantFoldersToo()
    {
        using Sut sut = Sut.Create();
        FolderRecord parent = sut.Folders.Create("Work");
        FolderRecord child = sut.Folders.Create("Clients", parent.Id);
        VaultItemRecord item = sut.Items.Create(new LoginItem("Acme Portal", "me", "pw", null, null), child.Id);

        sut.Folders.Delete(parent.Id);

        Assert.Null(sut.Folders.Get(child.Id));
        VaultItemRecord? afterDelete = sut.Items.Get(item.Id);
        Assert.NotNull(afterDelete);
        Assert.Null(afterDelete!.FolderId);
    }
}
