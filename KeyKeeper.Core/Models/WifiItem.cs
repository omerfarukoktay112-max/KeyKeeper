namespace KeyKeeper.Core.Models;

/// <summary>
/// A Wi-Fi network's join credentials. <see cref="Password"/> is the sensitive field here (see
/// <see cref="IVaultItemPayload"/>'s doc comment for the memory-hygiene tradeoff). <c>Open</c>
/// networks legitimately have no password -- that's why it's nullable, not a validation gap.
/// </summary>
public sealed record WifiItem(
    string Title,
    string Ssid,
    string? Password,
    WifiSecurityProtocol SecurityProtocol,
    bool IsHidden) : IVaultItemPayload
{
    public static VaultItemType ItemType => VaultItemType.WiFi;
}
