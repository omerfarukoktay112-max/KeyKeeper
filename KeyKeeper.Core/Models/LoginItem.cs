namespace KeyKeeper.Core.Models;

/// <summary>
/// A website/app login. <see cref="TotpSecret"/> is stored (Base32, as issuers normally hand it
/// out) but nothing in this phase generates a TOTP code from it yet -- that's the later "Araçlar"
/// phase. <see cref="Password"/> and <see cref="TotpSecret"/> are the sensitive fields here (see
/// <see cref="IVaultItemPayload"/>'s doc comment for the memory-hygiene tradeoff).
/// </summary>
public sealed record LoginItem(
    string Title,
    string? Username,
    string? Password,
    string? Url,
    string? TotpSecret) : IVaultItemPayload
{
    public static VaultItemType ItemType => VaultItemType.Login;
}
