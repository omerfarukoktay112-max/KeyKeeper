namespace KeyKeeper.Core.Models;

/// <summary>
/// Marker implemented by every vault item payload type (<see cref="LoginItem"/>,
/// <see cref="CardItem"/>, <see cref="WifiItem"/>, <see cref="SecureNoteItem"/>,
/// <see cref="LicenseItem"/>). The static abstract <see cref="ItemType"/> lets
/// <c>KeyKeeper.Core.Data.IVaultItemRepository</c>'s generic methods (e.g.
/// <c>Create&lt;TPayload&gt;</c>) look up the right <see cref="VaultItemType"/> to store in
/// <c>VaultItems.ItemType</c> from the payload's CLR type alone, instead of callers having to
/// pass it separately (and risk it disagreeing with the actual payload).
///
/// These types are plain <c>System.Text.Json</c>-serializable records — no custom converters,
/// no encryption-awareness of their own. Encryption happens one level up, in the repository: see
/// its doc comment for how the serialize-then-encrypt-then-zero sequence balances memory hygiene
/// against the fact that individual sensitive fields here (passwords, CVVs, card numbers, ...)
/// are ordinary immutable <see cref="string"/> properties that can't themselves be wiped -- the
/// same acknowledged string-boundary tradeoff as <c>KeyKeeper.Core.Storage.WrappedKeyStorage</c>'s
/// Base64 comment and <c>SetupViewModel.Password</c>, not something this layer pretends to solve.
/// </summary>
public interface IVaultItemPayload
{
    static abstract VaultItemType ItemType { get; }

    /// <summary>The user-given display name shown in lists/titles — every item type has one
    /// (it's the only thing this generic-over-payload-type UI layer can assume about an
    /// otherwise-opaque payload without a type-specific switch).</summary>
    string Title { get; }
}
