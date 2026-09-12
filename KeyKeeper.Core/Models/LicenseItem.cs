namespace KeyKeeper.Core.Models;

/// <summary>
/// A software/service license or ID key. <see cref="LicenseKey"/> is the sensitive field here
/// (see <see cref="IVaultItemPayload"/>'s doc comment for the memory-hygiene tradeoff).
/// <see cref="ExpiresAtUtc"/> is optional -- plenty of licenses (perpetual, lifetime) never
/// expire.
/// </summary>
public sealed record LicenseItem(
    string Title,
    string? LicenseKey,
    string? SoftwareName,
    DateTimeOffset? ExpiresAtUtc) : IVaultItemPayload
{
    public static VaultItemType ItemType => VaultItemType.License;
}
