using KeyKeeper.Core.Models;

namespace KeyKeeper.Core.Data;

/// <summary>
/// Builds the exact text a vault item is searchable by -- the title plus a handful of non-secret
/// descriptive fields per type (username, URL, cardholder name, card brand, SSID, software name)
/// -- deliberately never touching a payload's actual secret fields (password, CVV, license key,
/// Wi-Fi password) or <see cref="SecureNoteItem.Content"/>.
///
/// A security requirement, not an oversight: search runs over the already-decrypted item list the
/// UI holds in memory for display anyway, so nothing here decrypts anything extra -- but if the
/// searchable set ever grew to include a secret field, every keystroke while searching would
/// linger that field's plaintext through a filtered results list far more casually than the
/// deliberate per-field "Göster" reveal toggle each form already requires before showing it at
/// all. Kept in <c>KeyKeeper.Core</c>, not the MAUI head project's <c>VaultItemListEntry</c> (the
/// only caller), specifically so this exact claim is unit-testable without a UI test harness.
/// </summary>
public static class VaultItemSearchText
{
    public static string Build(VaultItemType type, object payload) => type switch
    {
        VaultItemType.Login when payload is LoginItem l => Join(l.Title, l.Username, l.Url),
        VaultItemType.Card when payload is CardItem c => Join(c.Title, c.CardholderName, c.CardType.ToString()),
        VaultItemType.WiFi when payload is WifiItem w => Join(w.Title, w.Ssid),
        VaultItemType.Note when payload is SecureNoteItem n => Join(n.Title),
        VaultItemType.License when payload is LicenseItem lic => Join(lic.Title, lic.SoftwareName),
        _ => string.Empty,
    };

    private static string Join(params string?[] fields) =>
        string.Join(' ', fields.Where(f => !string.IsNullOrEmpty(f)));
}
