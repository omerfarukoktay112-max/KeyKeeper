using KeyKeeper.Core.Models;

namespace KeyKeeper.Core.Data;

/// <summary>
/// A decrypted vault item as returned by <see cref="IVaultItemRepository"/>. <see cref="Payload"/>
/// is boxed as <see cref="object"/>, not a generic type parameter, because <see cref="IVaultItemRepository.List"/>
/// returns a heterogeneous mix of item types in one call (a vault item list is heterogeneous by
/// nature) — its actual runtime type is always the one <see cref="ItemType"/> identifies (one of
/// <see cref="LoginItem"/>, <see cref="CardItem"/>, <see cref="WifiItem"/>,
/// <see cref="SecureNoteItem"/>, <see cref="LicenseItem"/>), so callers pattern-match on it.
/// </summary>
public sealed record VaultItemRecord(
    string Id,
    VaultItemType ItemType,
    string? FolderId,
    bool IsFavorite,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<string> Tags,
    object Payload);
