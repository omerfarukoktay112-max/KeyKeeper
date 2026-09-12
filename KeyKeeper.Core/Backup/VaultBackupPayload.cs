namespace KeyKeeper.Core.Backup;

/// <summary>
/// The whole-vault contents encrypted inside a .vault backup — every folder, tag, and item, as a
/// single JSON object that gets serialized and then handed to
/// <c>KeyKeeper.Core.Crypto.IEncryptionService.Encrypt</c> as one buffer. See
/// <see cref="VaultBackupHeader"/> for the (unencrypted) header that precedes it and
/// <see cref="VaultBackupFile"/> for the exact on-disk framing of the two together.
/// </summary>
public sealed record VaultBackupPayload(
    IReadOnlyList<BackupFolder> Folders,
    IReadOnlyList<BackupTag> Tags,
    IReadOnlyList<BackupItem> Items);
