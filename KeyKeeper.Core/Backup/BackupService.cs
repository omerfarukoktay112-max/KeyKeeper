using System.Text.Json;
using KeyKeeper.Core.Auth;
using KeyKeeper.Core.Crypto;
using KeyKeeper.Core.Data;
using SQLite;

namespace KeyKeeper.Core.Backup;

/// <summary>
/// <see cref="IBackupService"/>. Exporting reads through the already-tested repositories
/// (<c>IVaultItemRepository</c>/<c>IFolderRepository</c>/<c>ITagRepository</c>) — every item
/// comes back already decrypted under the live vault key, so this class never touches
/// <c>VaultItems.EncryptedPayload</c> directly on the way out. Importing instead writes the
/// <c>Folders</c>/<c>Tags</c>/<c>VaultItems</c>/<c>ItemTags</c> tables directly over
/// <c>IVaultDatabaseService</c>'s raw connection — the repositories' <c>Create</c> methods always
/// assign a fresh id and "now" timestamps, which would silently discard the backup's original
/// ids/timestamps/favorite flags on restore.
///
/// Key separation, on export: <see cref="ExportAsync"/> derives a completely fresh Argon2id key
/// (a new random salt, the current <c>Argon2Parameters.Default</c> cost) for the backup's own
/// encryption, rather than reusing the live vault's own salt from <c>IVaultUnlockStore</c> (which
/// would derive the exact same key SQLCipher already uses for the vault database file). A backup
/// is a separate artifact with its own lifecycle — it can end up copied to cloud storage, emailed,
/// stuck on an old phone — and giving it independent key material means a compromise of one
/// (backup file or live vault) doesn't directly hand over the key to the other, even though both
/// are ultimately derived from the same master password. This costs a second Argon2id derivation
/// per export (one to verify the entered password against the live vault's verifier, one to
/// derive the backup's own key) — an explicit, infrequent, user-initiated operation, not a hot
/// path, so paying that cost twice is the right tradeoff over reusing one derivation.
/// </summary>
public sealed class BackupService : IBackupService
{
    private readonly IVaultItemRepository _items;
    private readonly IFolderRepository _folders;
    private readonly ITagRepository _tags;
    private readonly IVaultUnlockStore _unlockStore;
    private readonly IVaultDatabaseService _vaultDatabase;
    private readonly IAuthenticationService _auth;
    private readonly IKeyDerivationService _kdf;
    private readonly IMasterPasswordVerifier _verifier;
    private readonly IEncryptionService _encryption;
    private readonly IBackupFormatMigrator _formatMigrator;

    public BackupService(
        IVaultItemRepository items,
        IFolderRepository folders,
        ITagRepository tags,
        IVaultUnlockStore unlockStore,
        IVaultDatabaseService vaultDatabase,
        IAuthenticationService auth,
        IKeyDerivationService kdf,
        IMasterPasswordVerifier verifier,
        IEncryptionService encryption,
        IBackupFormatMigrator formatMigrator)
    {
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _folders = folders ?? throw new ArgumentNullException(nameof(folders));
        _tags = tags ?? throw new ArgumentNullException(nameof(tags));
        _unlockStore = unlockStore ?? throw new ArgumentNullException(nameof(unlockStore));
        _vaultDatabase = vaultDatabase ?? throw new ArgumentNullException(nameof(vaultDatabase));
        _auth = auth ?? throw new ArgumentNullException(nameof(auth));
        _kdf = kdf ?? throw new ArgumentNullException(nameof(kdf));
        _verifier = verifier ?? throw new ArgumentNullException(nameof(verifier));
        _encryption = encryption ?? throw new ArgumentNullException(nameof(encryption));
        _formatMigrator = formatMigrator ?? throw new ArgumentNullException(nameof(formatMigrator));
    }

    public Task<BackupExportResult> ExportAsync(char[] masterPassword)
    {
        ArgumentNullException.ThrowIfNull(masterPassword);

        return Task.Run(() =>
        {
            if (!VerifyAgainstLiveVault(masterPassword))
            {
                return BackupExportResult.WrongPassword;
            }

            byte[] backupSalt = _kdf.GenerateSalt();
            Argon2Parameters backupParams = Argon2Parameters.Default;
            byte[] backupKey = _kdf.DeriveKey(masterPassword, backupSalt, backupParams);
            try
            {
                byte[] json = JsonSerializer.SerializeToUtf8Bytes(BuildPayload());
                byte[] encryptedPayload;
                try
                {
                    encryptedPayload = _encryption.Encrypt(json, backupKey);
                }
                finally
                {
                    SecureMemoryHelper.Clear(json);
                }

                var header = new VaultBackupHeader(
                    _formatMigrator.CurrentFormatVersion,
                    backupSalt,
                    backupParams.MemorySizeKiB,
                    backupParams.Iterations,
                    backupParams.Parallelism,
                    backupParams.OutputLengthBytes,
                    DateTimeOffset.UtcNow);

                byte[] file = VaultBackupFile.Pack(header, encryptedPayload);
                return new BackupExportResult(BackupExportResultKind.Success, file);
            }
            finally
            {
                SecureMemoryHelper.Clear(backupKey);
            }
        });
    }

    public Task<bool> VerifyMasterPasswordAsync(char[] masterPassword)
    {
        ArgumentNullException.ThrowIfNull(masterPassword);
        return Task.Run(() => VerifyAgainstLiveVault(masterPassword));
    }

    public Task<BackupImportResult> PrepareImportAsync(string filePath, char[] masterPassword)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(masterPassword);

        return Task.Run(() =>
        {
            byte[] fileBytes = File.ReadAllBytes(filePath);

            VaultBackupHeader header;
            byte[] encryptedPayload;
            try
            {
                (header, encryptedPayload) = VaultBackupFile.Unpack(fileBytes);
            }
            catch (VaultBackupFormatException ex)
            {
                return BackupImportResult.CorruptFile(ex.Message);
            }

            try
            {
                _formatMigrator.EnsureSupported(header.FormatVersion);
            }
            catch (UnsupportedBackupVersionException ex)
            {
                return BackupImportResult.UnsupportedVersion(ex.Message);
            }

            var backupParams = new Argon2Parameters(
                header.Argon2MemorySizeKiB, header.Argon2Iterations, header.Argon2Parallelism, header.Argon2OutputLengthBytes);
            byte[] key = _kdf.DeriveKey(masterPassword, header.Salt, backupParams);

            byte[] json;
            try
            {
                json = _encryption.Decrypt(encryptedPayload, key);
            }
            catch (VaultCryptographyException)
            {
                // AES-GCM's authentication tag can't distinguish "wrong key" from "tampered
                // ciphertext" -- see VaultCryptographyException's own callers elsewhere in this
                // codebase -- so a truncated-but-still-long-enough payload is reported the same
                // way a genuinely wrong password would be.
                return BackupImportResult.WrongPassword();
            }
            catch (ArgumentException)
            {
                // Too short to even contain a nonce + tag (see AesGcmEncryptionService.Decrypt) --
                // only reachable with a payload shorter than 28 bytes, which no genuine .vault
                // file (however wrong the password) ever produces, so this is a corruption
                // signal, not a password one.
                return BackupImportResult.CorruptFile("Şifreli veri bloğu bozulmuş veya eksik.");
            }
            finally
            {
                SecureMemoryHelper.Clear(key);
            }

            try
            {
                VaultBackupPayload? payload = JsonSerializer.Deserialize<VaultBackupPayload>(json);
                if (payload is null)
                {
                    return BackupImportResult.CorruptFile("Yedek içeriği boş; dosya bozulmuş olabilir.");
                }

                return BackupImportResult.Success(
                    new BackupImportPreview(payload.Folders.Count, payload.Tags.Count, payload.Items.Count, payload));
            }
            catch (JsonException ex)
            {
                return BackupImportResult.CorruptFile($"Yedek içeriği okunamadı: {ex.Message}");
            }
            finally
            {
                SecureMemoryHelper.Clear(json);
            }
        });
    }

    public Task CommitImportAsync(BackupImportPreview preview)
    {
        ArgumentNullException.ThrowIfNull(preview);

        return Task.Run(() =>
        {
            SQLiteConnection connection = _vaultDatabase.GetOpenConnectionOrThrow();
            VaultBackupPayload payload = preview.Payload;

            // PRAGMA foreign_keys is a no-op once a transaction is already open (SQLite only
            // honors a change to it outside any pending BEGIN), so it has to be toggled off
            // *before* RunInTransaction starts, and restored after it ends -- this is what lets
            // the delete-everything-then-insert-everything below ignore FK insert order
            // (folders/tags/items can be inserted in whatever order the backup file lists them).
            connection.Execute("PRAGMA foreign_keys = OFF;");
            try
            {
                connection.RunInTransaction(() => ReplaceVaultContents(connection, payload));
            }
            finally
            {
                connection.Execute("PRAGMA foreign_keys = ON;");
            }
        });
    }

    private void ReplaceVaultContents(SQLiteConnection connection, VaultBackupPayload payload)
    {
        connection.Execute("DELETE FROM ItemTags;");
        connection.Execute("DELETE FROM VaultItems;");
        connection.Execute("DELETE FROM Tags;");
        connection.Execute("DELETE FROM Folders;");

        foreach (BackupFolder folder in payload.Folders)
        {
            connection.Execute(
                "INSERT INTO Folders (Id, Name, ParentFolderId) VALUES (?, ?, ?);",
                folder.Id, folder.Name, folder.ParentFolderId);
        }

        foreach (BackupTag tag in payload.Tags)
        {
            connection.Execute("INSERT INTO Tags (Id, Name) VALUES (?, ?);", tag.Id, tag.Name);
        }

        var tagIdByName = payload.Tags.ToDictionary(t => t.Name, t => t.Id, StringComparer.Ordinal);

        foreach (BackupItem item in payload.Items)
        {
            byte[] json = JsonSerializer.SerializeToUtf8Bytes(item.Payload);
            byte[] encryptedPayload;
            try
            {
                encryptedPayload = _auth.EncryptWithVaultKey(json);
            }
            finally
            {
                SecureMemoryHelper.Clear(json);
            }

            connection.Execute(
                """
                INSERT INTO VaultItems (Id, ItemType, FolderId, IsFavorite, CreatedAtUtc, UpdatedAtUtc, EncryptedPayload)
                VALUES (?, ?, ?, ?, ?, ?, ?);
                """,
                item.Id, (int)item.ItemType, item.FolderId, item.IsFavorite,
                item.CreatedAtUtc.UtcTicks, item.UpdatedAtUtc.UtcTicks, encryptedPayload);

            foreach (string tagName in item.Tags)
            {
                if (tagIdByName.TryGetValue(tagName, out string? tagId))
                {
                    connection.Execute("INSERT INTO ItemTags (ItemId, TagId) VALUES (?, ?);", item.Id, tagId);
                }
            }
        }
    }

    private VaultBackupPayload BuildPayload()
    {
        IReadOnlyList<FolderRecord> folders = _folders.ListAll();
        IReadOnlyList<TagRecord> tags = _tags.ListAll();
        IReadOnlyList<VaultItemRecord> items = _items.List();

        return new VaultBackupPayload(
            folders.Select(f => new BackupFolder(f.Id, f.Name, f.ParentFolderId)).ToList(),
            tags.Select(t => new BackupTag(t.Id, t.Name)).ToList(),
            items.Select(i => new BackupItem(
                i.Id,
                i.ItemType,
                i.FolderId,
                i.IsFavorite,
                i.CreatedAtUtc,
                i.UpdatedAtUtc,
                i.Tags,
                JsonSerializer.SerializeToElement(i.Payload, i.Payload.GetType()))).ToList());
    }

    /// <summary>Re-derives a key from the vault's own stored salt/parameters and checks it
    /// against the stored verifier -- the same check <c>AuthenticationService.UnlockWithMasterPasswordAsync</c>
    /// makes, reused here (rather than calling that method) so this never touches the lockout
    /// counter or re-opens the database: a mistyped password while exporting or confirming a CSV
    /// export shouldn't count as a failed vault-unlock attempt.</summary>
    private bool VerifyAgainstLiveVault(char[] masterPassword)
    {
        VaultUnlockMetadata metadata = _unlockStore.ReadMetadata()
            ?? throw new InvalidOperationException("The vault has not been initialized yet.");

        var kdfParams = new Argon2Parameters(
            metadata.Argon2MemorySizeKiB, metadata.Argon2Iterations, metadata.Argon2Parallelism, metadata.Argon2OutputLengthBytes);
        byte[] key = _kdf.DeriveKey(masterPassword, metadata.Salt, kdfParams);
        try
        {
            return _verifier.Verify(key, metadata.MasterPasswordVerifier);
        }
        finally
        {
            SecureMemoryHelper.Clear(key);
        }
    }
}
