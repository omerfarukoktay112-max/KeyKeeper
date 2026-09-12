using System.Text.Json;
using KeyKeeper.Core.Models;

namespace KeyKeeper.Core.Backup;

/// <summary>
/// A vault item as stored inside a .vault backup's decrypted payload — everything
/// <c>KeyKeeper.Core.Data.VaultItemRecord</c> carries, except <see cref="Payload"/> is a
/// <see cref="JsonElement"/> (the item's type-specific fields as nested JSON, e.g. a
/// <see cref="LoginItem"/>'s shape) rather than a boxed <c>object</c> — <c>VaultItemRecord</c>'s
/// <c>Payload</c> only carries a CLR type at runtime because
/// <c>KeyKeeper.Core.Data.VaultItemRepository</c> decrypts each item knowing its
/// <see cref="ItemType"/>; here it needs to round-trip through JSON either way (the payload was
/// encrypted, as JSON, inside the backup file), so keeping it as nested JSON avoids a pointless
/// extra encode/decode through a concrete C# type during import (see <c>BackupService</c>).
///
/// Memory hygiene note: unlike <c>VaultItemRepository</c>'s own decrypted buffers, the
/// <see cref="JsonElement"/>s here are backed by a <c>JsonDocument</c>-managed buffer that
/// <see cref="System.Text.Json"/> allocates internally when deserializing this record — there is
/// no public API to zero it once <c>BackupService</c> is done with it. This is the same
/// acknowledged string/managed-buffer-boundary tradeoff already noted on
/// <see cref="IVaultItemPayload"/> and <c>KeyKeeper.Core.Storage.WrappedKeyStorage</c>, not a gap
/// specific to backups.
/// </summary>
public sealed record BackupItem(
    string Id,
    VaultItemType ItemType,
    string? FolderId,
    bool IsFavorite,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<string> Tags,
    JsonElement Payload);
