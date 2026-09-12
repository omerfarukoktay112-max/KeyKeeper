using KeyKeeper.Core.Data;
using KeyKeeper.Core.Models;

namespace KeyKeeper.Core.Tests.Data;

/// <summary>
/// The regression suite for the search-safety requirement: search must only ever match a vault
/// item's non-secret descriptive fields, never a field a form masks behind a "Göster" reveal
/// toggle. Every test below asserts both halves -- the safe fields ARE included, and the secret
/// field is explicitly NOT -- so a future edit that accidentally widens the searchable set trips
/// a test immediately.
/// </summary>
public class VaultItemSearchTextTests
{
    [Fact]
    public void Build_LoginItem_IncludesTitleUsernameAndUrl_ExcludesPasswordAndTotpSecret()
    {
        var login = new LoginItem("GitHub", "octocat", "S3cretPass", "https://github.com", "BASE32SECRET");

        string searchText = VaultItemSearchText.Build(VaultItemType.Login, login);

        Assert.Contains("GitHub", searchText);
        Assert.Contains("octocat", searchText);
        Assert.Contains("https://github.com", searchText);
        Assert.DoesNotContain("S3cretPass", searchText);
        Assert.DoesNotContain("BASE32SECRET", searchText);
    }

    [Fact]
    public void Build_CardItem_IncludesTitleCardholderNameAndCardType_ExcludesCardNumberAndCvv()
    {
        var card = new CardItem("Ana Kartim", "John Doe", "4111111111111111", 12, 2028, "123", CardType.Visa);

        string searchText = VaultItemSearchText.Build(VaultItemType.Card, card);

        Assert.Contains("Ana Kartim", searchText);
        Assert.Contains("John Doe", searchText);
        Assert.Contains("Visa", searchText);
        Assert.DoesNotContain("4111111111111111", searchText);
        Assert.DoesNotContain("123", searchText);
    }

    [Fact]
    public void Build_WifiItem_IncludesTitleAndSsid_ExcludesPassword()
    {
        var wifi = new WifiItem("Ev WiFi", "EvAgi_5G", "WifiSifre123", WifiSecurityProtocol.Wpa2, false);

        string searchText = VaultItemSearchText.Build(VaultItemType.WiFi, wifi);

        Assert.Contains("Ev WiFi", searchText);
        Assert.Contains("EvAgi_5G", searchText);
        Assert.DoesNotContain("WifiSifre123", searchText);
    }

    [Fact]
    public void Build_SecureNoteItem_IncludesOnlyTitle_ExcludesContent()
    {
        var note = new SecureNoteItem("Wi-Fi Yedek Kodlari", "XJ4-9284 super secret recovery info");

        string searchText = VaultItemSearchText.Build(VaultItemType.Note, note);

        Assert.Contains("Wi-Fi Yedek Kodlari", searchText);
        Assert.DoesNotContain("XJ4-9284", searchText);
        Assert.DoesNotContain("secret recovery", searchText);
    }

    [Fact]
    public void Build_LicenseItem_IncludesTitleAndSoftwareName_ExcludesLicenseKey()
    {
        var license = new LicenseItem("Office 365", "XXXXX-XXXXX-XXXXX-XXXXX", "Microsoft", null);

        string searchText = VaultItemSearchText.Build(VaultItemType.License, license);

        Assert.Contains("Office 365", searchText);
        Assert.Contains("Microsoft", searchText);
        Assert.DoesNotContain("XXXXX-XXXXX-XXXXX-XXXXX", searchText);
    }

    [Fact]
    public void Build_NullOptionalFields_DoesNotThrowAndOmitsThem()
    {
        var login = new LoginItem("Minimal", null, null, null, null);

        string searchText = VaultItemSearchText.Build(VaultItemType.Login, login);

        Assert.Equal("Minimal", searchText);
    }
}
