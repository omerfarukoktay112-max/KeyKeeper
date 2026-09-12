using KeyKeeper.Core.Auth;
using KeyKeeper.Core.Backup;
using KeyKeeper.Core.Crypto;
using KeyKeeper.Core.Data;
using KeyKeeper.Core.Models;
using KeyKeeper.Core.Storage;
using KeyKeeper.Core.Tests.Data;

namespace KeyKeeper.Core.Tests.Backup;

[Collection(nameof(VaultDatabaseTestCollection))]
public class BackupServiceTests
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

    private sealed class InMemorySecureKeyValueStore : ISecureKeyValueStore
    {
        private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

        public Task SetAsync(string key, string value)
        {
            _values[key] = value;
            return Task.CompletedTask;
        }

        public Task<string?> GetAsync(string key) =>
            Task.FromResult(_values.TryGetValue(key, out string? value) ? value : null);

        public void Remove(string key) => _values.Remove(key);
    }

    private sealed class FakeBiometricAuthService : IBiometricAuthService
    {
        public Task<BiometricAvailability> CheckAvailabilityAsync() => Task.FromResult(BiometricAvailability.NotEnrolled);

        public Task<BiometricAuthOutcome> AuthenticateAsync(string reason) => Task.FromResult(BiometricAuthOutcome.NotAvailable);
    }

    /// <summary>
    /// One complete, real vault stack — real Argon2id/AES-GCM crypto, a real SQLite-backed vault
    /// database (plain provider; see <c>VaultDatabaseTestCollection</c>), real repositories, and a
    /// real <see cref="BackupService"/>. Round-trip tests below create <em>two</em> independent
    /// harnesses (a "source" being exported from, a "target" being restored into) specifically to
    /// prove that importing a backup never depends on the exporting vault's own database, key, or
    /// even still being reachable — exactly the "reset the app, restore on a fresh vault" scenario
    /// this feature exists for.
    /// </summary>
    private sealed class VaultHarness : IDisposable
    {
        private readonly IVaultDatabaseService _database;
        private readonly VaultUnlockStore _unlockStore;

        public TempPathProvider PathProvider { get; } = new();

        public AuthenticationService Auth { get; }

        public IVaultItemRepository Items { get; }

        public IFolderRepository Folders { get; }

        public ITagRepository Tags { get; }

        public BackupService Backup { get; }

        public VaultHarness()
        {
            _database = new VaultDatabaseService(PathProvider, new VaultMigrationService());
            _unlockStore = new VaultUnlockStore(PathProvider);
            var unlockStore = _unlockStore;
            var wrappedKeyStorage = new WrappedKeyStorage(new InMemorySecureKeyValueStore());
            var kdf = new Argon2idKeyDerivationService();
            var verifier = new MasterPasswordVerifier();
            var encryption = new AesGcmEncryptionService();

            Auth = new AuthenticationService(
                kdf, verifier, encryption, _database, unlockStore, wrappedKeyStorage,
                new FakeBiometricAuthService(), new InactivityTimerService());

            Items = new VaultItemRepository(_database, Auth);
            Folders = new FolderRepository(_database);
            Tags = new TagRepository(_database);
            Backup = new BackupService(Items, Folders, Tags, unlockStore, _database, Auth, kdf, verifier, encryption, new BackupFormatMigrator());
        }

        /// <summary>A path inside this harness's own temp directory, so it's cleaned up by
        /// <see cref="Dispose"/> along with everything else, same as the real vault/unlock
        /// database files.</summary>
        public string TempFilePath(string fileName) => Path.Combine(PathProvider.Directory, fileName);

        public void Dispose()
        {
            Auth.Dispose();
            _database.Dispose();
            _unlockStore.Dispose();
            PathProvider.Cleanup();
        }
    }

    private static char[] Password(string value) => value.ToCharArray();

    [Fact]
    public async Task ExportAsync_WrongPassword_ReturnsWrongPasswordWithoutThrowing()
    {
        using var harness = new VaultHarness();
        await harness.Auth.InitializeVaultAsync(Password("correct horse battery staple"));

        BackupExportResult result = await harness.Backup.ExportAsync(Password("wrong password"));

        Assert.Equal(BackupExportResultKind.WrongPassword, result.Kind);
        Assert.Null(result.FileBytes);
    }

    [Fact]
    public async Task ExportAsync_CorrectPassword_ProducesAFileThatRoundTripsThroughPrepareImport()
    {
        using var source = new VaultHarness();
        await source.Auth.InitializeVaultAsync(Password("correct horse battery staple"));
        source.Folders.Create("Work");
        VaultItemRecord note = source.Items.Create(new SecureNoteItem("Note", "very secret content"));
        source.Items.AddTag(note.Id, "important");

        BackupExportResult exportResult = await source.Backup.ExportAsync(Password("correct horse battery staple"));
        Assert.Equal(BackupExportResultKind.Success, exportResult.Kind);
        Assert.NotNull(exportResult.FileBytes);

        using var target = new VaultHarness();
        await target.Auth.InitializeVaultAsync(Password("some other password"));
        string filePath = target.TempFilePath("import.vault");
        File.WriteAllBytes(filePath, exportResult.FileBytes!);

        BackupImportResult importResult = await target.Backup.PrepareImportAsync(filePath, Password("correct horse battery staple"));

        Assert.Equal(BackupImportResultKind.Success, importResult.Kind);
        Assert.NotNull(importResult.Preview);
        Assert.Equal(1, importResult.Preview!.FolderCount);
        Assert.Equal(1, importResult.Preview!.TagCount);
        Assert.Equal(1, importResult.Preview!.ItemCount);
    }

    [Fact]
    public async Task CommitImportAsync_ReplacesTheTargetVaultsExistingContentsWithTheImportedData()
    {
        using var source = new VaultHarness();
        await source.Auth.InitializeVaultAsync(Password("correct horse battery staple"));
        string workFolderId = source.Folders.Create("Work").Id;
        VaultItemRecord login = source.Items.Create(
            new LoginItem("GitHub", "octocat", "hunter2", "https://github.com", null), folderId: workFolderId);
        source.Items.AddTag(login.Id, "dev");
        source.Items.SetFavorite(login.Id, true);

        BackupExportResult exportResult = await source.Backup.ExportAsync(Password("correct horse battery staple"));

        using var target = new VaultHarness();
        await target.Auth.InitializeVaultAsync(Password("target-only password"));
        VaultItemRecord preExisting = target.Items.Create(new SecureNoteItem("Old note", "should be gone after import"));

        string filePath = target.TempFilePath("import.vault");
        File.WriteAllBytes(filePath, exportResult.FileBytes!);
        BackupImportResult prepared = await target.Backup.PrepareImportAsync(filePath, Password("correct horse battery staple"));
        Assert.Equal(BackupImportResultKind.Success, prepared.Kind);

        await target.Backup.CommitImportAsync(prepared.Preview!);

        // Replace, not merge (a deliberate MVP scope decision -- see BackupImportPreview): the
        // target vault's own pre-existing item is gone entirely.
        Assert.Null(target.Items.Get(preExisting.Id));

        IReadOnlyList<VaultItemRecord> importedItems = target.Items.List();
        VaultItemRecord onlyItem = Assert.Single(importedItems);
        Assert.Equal(login.Id, onlyItem.Id);
        Assert.Equal(login.Payload, onlyItem.Payload);
        Assert.True(onlyItem.IsFavorite);
        Assert.Equal(["dev"], onlyItem.Tags);
        Assert.Equal(workFolderId, onlyItem.FolderId);

        FolderRecord importedFolder = Assert.Single(target.Folders.ListAll());
        Assert.Equal(workFolderId, importedFolder.Id);
        Assert.Equal("Work", importedFolder.Name);
    }

    [Fact]
    public async Task VerifyMasterPasswordAsync_CorrectPassword_ReturnsTrue()
    {
        using var harness = new VaultHarness();
        await harness.Auth.InitializeVaultAsync(Password("correct horse battery staple"));

        Assert.True(await harness.Backup.VerifyMasterPasswordAsync(Password("correct horse battery staple")));
    }

    [Fact]
    public async Task VerifyMasterPasswordAsync_WrongPassword_ReturnsFalse()
    {
        using var harness = new VaultHarness();
        await harness.Auth.InitializeVaultAsync(Password("correct horse battery staple"));

        Assert.False(await harness.Backup.VerifyMasterPasswordAsync(Password("wrong")));
    }

    [Fact]
    public async Task PrepareImportAsync_WrongPassword_ReturnsWrongPasswordAndNoPreview()
    {
        using var source = new VaultHarness();
        await source.Auth.InitializeVaultAsync(Password("correct horse battery staple"));
        source.Items.Create(new SecureNoteItem("Note", "content"));
        BackupExportResult exportResult = await source.Backup.ExportAsync(Password("correct horse battery staple"));

        using var target = new VaultHarness();
        await target.Auth.InitializeVaultAsync(Password("whatever"));
        string filePath = target.TempFilePath("import.vault");
        File.WriteAllBytes(filePath, exportResult.FileBytes!);

        BackupImportResult result = await target.Backup.PrepareImportAsync(filePath, Password("totally wrong password"));

        Assert.Equal(BackupImportResultKind.WrongPassword, result.Kind);
        Assert.Null(result.Preview);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task PrepareImportAsync_UnrelatedRandomFile_ReturnsCorruptFileWithoutThrowing()
    {
        using var target = new VaultHarness();
        await target.Auth.InitializeVaultAsync(Password("whatever"));
        string filePath = target.TempFilePath("garbage.vault");
        File.WriteAllBytes(filePath, [1, 2, 3, 4, 5]);

        BackupImportResult result = await target.Backup.PrepareImportAsync(filePath, Password("whatever"));

        Assert.Equal(BackupImportResultKind.CorruptFile, result.Kind);
        Assert.Null(result.Preview);
    }

    [Fact]
    public async Task PrepareImportAsync_FileTruncatedMidHeader_ReturnsCorruptFileWithoutThrowing()
    {
        using var source = new VaultHarness();
        await source.Auth.InitializeVaultAsync(Password("correct horse battery staple"));
        source.Items.Create(new SecureNoteItem("Note", "content"));
        BackupExportResult exportResult = await source.Backup.ExportAsync(Password("correct horse battery staple"));

        // Magic (8) + length prefix (4) + a handful of header bytes -- the length prefix (read
        // from the real file) claims a much longer header than the 10 bytes that follow it here,
        // so Unpack must reject this as corrupt rather than reading past the end of the array.
        byte[] truncated = exportResult.FileBytes![..22];

        using var target = new VaultHarness();
        await target.Auth.InitializeVaultAsync(Password("whatever"));
        string filePath = target.TempFilePath("truncated.vault");
        File.WriteAllBytes(filePath, truncated);

        BackupImportResult result = await target.Backup.PrepareImportAsync(filePath, Password("correct horse battery staple"));

        Assert.Equal(BackupImportResultKind.CorruptFile, result.Kind);
        Assert.Null(result.Preview);
    }

    [Fact]
    public async Task PrepareImportAsync_FormatVersionNewerThanSupported_ReturnsUnsupportedVersion()
    {
        using var source = new VaultHarness();
        await source.Auth.InitializeVaultAsync(Password("correct horse battery staple"));
        source.Items.Create(new SecureNoteItem("Note", "content"));
        BackupExportResult exportResult = await source.Backup.ExportAsync(Password("correct horse battery staple"));

        (VaultBackupHeader header, byte[] encryptedPayload) = VaultBackupFile.Unpack(exportResult.FileBytes!);
        VaultBackupHeader futureHeader = header with { FormatVersion = 999 };
        byte[] tamperedFile = VaultBackupFile.Pack(futureHeader, encryptedPayload);

        using var target = new VaultHarness();
        await target.Auth.InitializeVaultAsync(Password("whatever"));
        string filePath = target.TempFilePath("future.vault");
        File.WriteAllBytes(filePath, tamperedFile);

        BackupImportResult result = await target.Backup.PrepareImportAsync(filePath, Password("correct horse battery staple"));

        Assert.Equal(BackupImportResultKind.UnsupportedVersion, result.Kind);
        Assert.Null(result.Preview);
    }
}
