using KeyKeeper.Core.Data;
using KeyKeeper.Core.Models;
using Microsoft.Maui.Graphics;

namespace KeyKeeper.ViewModels;

/// <summary>
/// A decrypted <see cref="VaultItemRecord"/> flattened for <c>VaultListPage</c>'s heterogeneous
/// list — one title/subtitle/icon/color per row, computed once via <see cref="From"/> instead of
/// the list's <c>DataTemplate</c> having to pattern-match <see cref="VaultItemRecord.Payload"/>
/// itself. Icon/color are a single emoji + a flat color per type — a placeholder pairing, not a
/// design pass; swapping in real iconography is cosmetic, not structural.
///
/// <see cref="FolderId"/>/<see cref="Tags"/>/<see cref="IsFavorite"/> (Phase 5) let
/// <c>VaultListViewModel</c> filter the already-loaded list client-side (by folder, by tag, to
/// favorites-only, combined) without a second repository round trip per filter change.
/// <see cref="SearchText"/> is <see cref="VaultItemSearchText"/>'s output, computed once here
/// rather than per-keystroke in the search box — see that type's doc comment for why it only ever
/// contains non-secret fields.
/// </summary>
public sealed record VaultItemListEntry(
    string Id,
    VaultItemType ItemType,
    string Title,
    string Subtitle,
    string Icon,
    Color Color,
    string? FolderId,
    IReadOnlyList<string> Tags,
    bool IsFavorite,
    string SearchText)
{
    public static VaultItemListEntry From(VaultItemRecord record)
    {
        (string title, string subtitle) = record.Payload switch
        {
            LoginItem l => (l.Title, l.Username ?? string.Empty),
            CardItem c => (c.Title, MaskCardNumber(c.CardNumber)),
            WifiItem w => (w.Title, w.Ssid),
            SecureNoteItem n => (n.Title, "Güvenli not"),
            LicenseItem lic => (lic.Title, lic.SoftwareName ?? string.Empty),
            _ => (record.Id, string.Empty),
        };

        (string icon, Color color) = record.ItemType switch
        {
            VaultItemType.Login => ("🔑", Color.FromArgb("#3B82F6")),
            VaultItemType.Card => ("💳", Color.FromArgb("#F59E0B")),
            VaultItemType.WiFi => ("📶", Color.FromArgb("#10B981")),
            VaultItemType.Note => ("📝", Color.FromArgb("#8B5CF6")),
            VaultItemType.License => ("🔖", Color.FromArgb("#06B6D4")),
            _ => ("❓", Colors.Gray),
        };

        string searchText = VaultItemSearchText.Build(record.ItemType, record.Payload);

        return new VaultItemListEntry(
            record.Id, record.ItemType, title, subtitle, icon, color,
            record.FolderId, record.Tags, record.IsFavorite, searchText);
    }

    /// <summary>Shows only the last 4 digits, even in the list — masking the full number by
    /// default isn't only the detail form's concern.</summary>
    private static string MaskCardNumber(string? cardNumber) =>
        string.IsNullOrEmpty(cardNumber) || cardNumber.Length < 4
            ? cardNumber ?? string.Empty
            : $"•••• {cardNumber[^4..]}";
}
