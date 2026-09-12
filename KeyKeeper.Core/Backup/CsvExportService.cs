using System.Globalization;
using System.Text;
using KeyKeeper.Core.Data;
using KeyKeeper.Core.Models;

namespace KeyKeeper.Core.Backup;

/// <summary>
/// <see cref="ICsvExportService"/>. One flat CSV, one row per item, with the union of every item
/// type's fields as columns (a per-type-only field is blank on every row of a different type) —
/// chosen over five separate per-type CSVs or a nested format, matching what other password
/// managers' plaintext CSV exports typically do and keeping "open this in a spreadsheet" genuinely
/// simple. Hand-rolled RFC 4180 quoting (no CsvHelper-style dependency) since the format needed
/// here is small and fixed: a field is wrapped in double quotes, with any internal double quote
/// doubled, whenever it contains a comma, a double quote, or a newline.
///
/// Column order: <c>Type, Title, IsFavorite, FolderId, Tags,</c> then every type-specific field
/// (Login, Card, WiFi, Note, License, in that order) — see <see cref="ColumnHeaders"/>.
/// </summary>
public sealed class CsvExportService : ICsvExportService
{
    private static readonly string[] ColumnHeaders =
    [
        "Type", "Title", "IsFavorite", "FolderId", "Tags",
        "Username", "Password", "Url", "TotpSecret",
        "CardholderName", "CardNumber", "ExpiryMonth", "ExpiryYear", "Cvv", "CardType",
        "Ssid", "WifiPassword", "SecurityProtocol", "IsHidden",
        "NoteContent",
        "LicenseKey", "SoftwareName", "ExpiresAtUtc",
    ];

    public string Export(IReadOnlyList<VaultItemRecord> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var sb = new StringBuilder();
        AppendRow(sb, ColumnHeaders);

        foreach (VaultItemRecord item in items)
        {
            AppendRow(sb, BuildRow(item));
        }

        return sb.ToString();
    }

    private static string[] BuildRow(VaultItemRecord item)
    {
        string type = item.ItemType.ToString();
        string title = TitleOf(item.Payload);
        string isFavorite = item.IsFavorite ? "true" : "false";
        string folderId = item.FolderId ?? string.Empty;
        string tags = string.Join(';', item.Tags);

        string username = string.Empty, password = string.Empty, url = string.Empty, totpSecret = string.Empty;
        string cardholderName = string.Empty, cardNumber = string.Empty, expiryMonth = string.Empty,
            expiryYear = string.Empty, cvv = string.Empty, cardType = string.Empty;
        string ssid = string.Empty, wifiPassword = string.Empty, securityProtocol = string.Empty, isHidden = string.Empty;
        string noteContent = string.Empty;
        string licenseKey = string.Empty, softwareName = string.Empty, expiresAtUtc = string.Empty;

        switch (item.Payload)
        {
            case LoginItem login:
                username = login.Username ?? string.Empty;
                password = login.Password ?? string.Empty;
                url = login.Url ?? string.Empty;
                totpSecret = login.TotpSecret ?? string.Empty;
                break;
            case CardItem card:
                cardholderName = card.CardholderName ?? string.Empty;
                cardNumber = card.CardNumber ?? string.Empty;
                expiryMonth = card.ExpiryMonth?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
                expiryYear = card.ExpiryYear?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
                cvv = card.Cvv ?? string.Empty;
                cardType = card.CardType.ToString();
                break;
            case WifiItem wifi:
                ssid = wifi.Ssid;
                wifiPassword = wifi.Password ?? string.Empty;
                securityProtocol = wifi.SecurityProtocol.ToString();
                isHidden = wifi.IsHidden ? "true" : "false";
                break;
            case SecureNoteItem note:
                noteContent = note.Content;
                break;
            case LicenseItem license:
                licenseKey = license.LicenseKey ?? string.Empty;
                softwareName = license.SoftwareName ?? string.Empty;
                expiresAtUtc = license.ExpiresAtUtc?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty;
                break;
        }

        return
        [
            type, title, isFavorite, folderId, tags,
            username, password, url, totpSecret,
            cardholderName, cardNumber, expiryMonth, expiryYear, cvv, cardType,
            ssid, wifiPassword, securityProtocol, isHidden,
            noteContent,
            licenseKey, softwareName, expiresAtUtc,
        ];
    }

    private static string TitleOf(object payload) => payload switch
    {
        IVaultItemPayload p => p.Title,
        _ => string.Empty,
    };

    private static void AppendRow(StringBuilder sb, IReadOnlyList<string> fields)
    {
        for (int i = 0; i < fields.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            sb.Append(EscapeField(fields[i]));
        }

        sb.Append("\r\n");
    }

    private static string EscapeField(string field)
    {
        bool needsQuoting = field.IndexOfAny([',', '"', '\r', '\n']) >= 0;
        if (!needsQuoting)
        {
            return field;
        }

        return $"\"{field.Replace("\"", "\"\"")}\"";
    }
}
