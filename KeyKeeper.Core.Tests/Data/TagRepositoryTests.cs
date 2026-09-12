using KeyKeeper.Core.Auth;
using KeyKeeper.Core.Crypto;
using KeyKeeper.Core.Data;
using KeyKeeper.Core.Models;

namespace KeyKeeper.Core.Tests.Data;

[Collection(nameof(VaultDatabaseTestCollection))]
public class TagRepositoryTests
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
        public required TagRepository Tags { get; init; }
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
                Tags = new TagRepository(database),
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
    public void Create_ThenGet_RoundTrips()
    {
        using Sut sut = Sut.Create();

        TagRecord created = sut.Tags.Create("Work");
        TagRecord? fetched = sut.Tags.Get(created.Id);

        Assert.NotNull(fetched);
        Assert.Equal("Work", fetched!.Name);
    }

    [Fact]
    public void Create_DuplicateName_ThrowsArgumentException()
    {
        using Sut sut = Sut.Create();
        sut.Tags.Create("Work");

        Assert.Throws<ArgumentException>(() => sut.Tags.Create("Work"));
    }

    [Fact]
    public void Get_NoSuchId_ReturnsNull()
    {
        using Sut sut = Sut.Create();

        Assert.Null(sut.Tags.Get("no-such-id"));
    }

    [Fact]
    public void ListAll_ReturnsEveryTagAlphabetical()
    {
        using Sut sut = Sut.Create();
        sut.Tags.Create("Work");
        sut.Tags.Create("Personal");

        IReadOnlyList<TagRecord> all = sut.Tags.ListAll();

        Assert.Equal(["Personal", "Work"], all.Select(t => t.Name));
    }

    [Fact]
    public void Rename_UpdatesTheName()
    {
        using Sut sut = Sut.Create();
        TagRecord tag = sut.Tags.Create("Work");

        sut.Tags.Rename(tag.Id, "Job");

        Assert.Equal("Job", sut.Tags.Get(tag.Id)!.Name);
    }

    [Fact]
    public void Rename_NoSuchId_ThrowsKeyNotFoundException()
    {
        using Sut sut = Sut.Create();

        Assert.Throws<KeyNotFoundException>(() => sut.Tags.Rename("no-such-id", "Job"));
    }

    [Fact]
    public void Rename_ToAnAlreadyUsedName_ThrowsArgumentException()
    {
        using Sut sut = Sut.Create();
        sut.Tags.Create("Work");
        TagRecord personal = sut.Tags.Create("Personal");

        Assert.Throws<ArgumentException>(() => sut.Tags.Rename(personal.Id, "Work"));
    }

    [Fact]
    public void Delete_RemovesTheTag()
    {
        using Sut sut = Sut.Create();
        TagRecord tag = sut.Tags.Create("Work");

        sut.Tags.Delete(tag.Id);

        Assert.Null(sut.Tags.Get(tag.Id));
    }

    [Fact]
    public void Delete_NoSuchId_DoesNotThrow()
    {
        using Sut sut = Sut.Create();

        sut.Tags.Delete("no-such-id");
    }

    [Fact]
    public void Delete_RemovesItFromEveryItemThatHadIt()
    {
        using Sut sut = Sut.Create();
        TagRecord tag = sut.Tags.Create("Work");
        VaultItemRecord item = sut.Items.Create(new LoginItem("Email", "me", "pw", null, null));
        sut.Items.AddTag(item.Id, "Work");

        sut.Tags.Delete(tag.Id);

        VaultItemRecord? afterDelete = sut.Items.Get(item.Id);
        Assert.Empty(afterDelete!.Tags);
    }

    [Fact]
    public void DeleteUnused_RemovesOnlyTagsAttachedToNoItems()
    {
        using Sut sut = Sut.Create();
        TagRecord used = sut.Tags.Create("Work");
        TagRecord unused = sut.Tags.Create("Someday");
        VaultItemRecord item = sut.Items.Create(new LoginItem("Email", "me", "pw", null, null));
        sut.Items.AddTag(item.Id, "Work");

        int deletedCount = sut.Tags.DeleteUnused();

        Assert.Equal(1, deletedCount);
        Assert.NotNull(sut.Tags.Get(used.Id));
        Assert.Null(sut.Tags.Get(unused.Id));
    }
}
