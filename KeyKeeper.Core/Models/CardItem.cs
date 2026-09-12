namespace KeyKeeper.Core.Models;

/// <summary>
/// A payment card. <see cref="CardNumber"/> and <see cref="Cvv"/> are the sensitive fields here
/// (see <see cref="IVaultItemPayload"/>'s doc comment for the memory-hygiene tradeoff).
/// <see cref="ExpiryMonth"/>/<see cref="ExpiryYear"/> are separate ints rather than a single
/// "MM/YY" string, so callers don't need to parse/validate a formatted string just to check
/// whether a card has expired.
/// </summary>
public sealed record CardItem(
    string Title,
    string? CardholderName,
    string? CardNumber,
    int? ExpiryMonth,
    int? ExpiryYear,
    string? Cvv,
    CardType CardType) : IVaultItemPayload
{
    public static VaultItemType ItemType => VaultItemType.Card;
}
