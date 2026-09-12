using KeyKeeper.Core.Backup;
using KeyKeeper.Core.Data;
using KeyKeeper.Core.Models;

namespace KeyKeeper.Core.Tests.Backup;

public class CsvExportServiceTests
{
    private readonly CsvExportService _sut = new();

    private static VaultItemRecord ItemOf(VaultItemType type, object payload, IReadOnlyList<string>? tags = null) =>
        new("item-1", type, null, false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, tags ?? [], payload);

    private static string DataRow(string csv)
    {
        string[] lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        return Assert.Single(lines[1..]);
    }

    [Fact]
    public void Export_HeaderRow_ListsEveryItemTypesColumns()
    {
        string csv = _sut.Export([]);
        string headerRow = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries)[0];

        foreach (string expectedColumn in new[]
        {
            "Type", "Title", "IsFavorite", "FolderId", "Tags",
            "Username", "Password", "Url", "TotpSecret",
            "CardholderName", "CardNumber", "ExpiryMonth", "ExpiryYear", "Cvv", "CardType",
            "Ssid", "WifiPassword", "SecurityProtocol", "IsHidden",
            "NoteContent",
            "LicenseKey", "SoftwareName", "ExpiresAtUtc",
        })
        {
            Assert.Contains(expectedColumn, headerRow);
        }
    }

    [Fact]
    public void Export_NoItems_ProducesOnlyTheHeaderRow()
    {
        string csv = _sut.Export([]);

        string[] lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Single(lines);
    }

    [Fact]
    public void Export_LoginItem_IncludesLoginFieldsInTheClear()
    {
        var login = new LoginItem("GitHub", "octocat", "hunter2", "https://github.com", "JBSWY3DPEHPK3PXP");
        string row = DataRow(_sut.Export([ItemOf(VaultItemType.Login, login, ["dev"])]));

        Assert.Contains("Login", row);
        Assert.Contains("GitHub", row);
        Assert.Contains("octocat", row);
        Assert.Contains("hunter2", row);
        Assert.Contains("https://github.com", row);
        Assert.Contains("JBSWY3DPEHPK3PXP", row);
        Assert.Contains("dev", row);
    }

    [Fact]
    public void Export_CardItem_IncludesCardFieldsInTheClear()
    {
        var card = new CardItem("Personal Visa", "Ada Lovelace", "4111111111111111", 12, 2030, "123", CardType.Visa);
        string row = DataRow(_sut.Export([ItemOf(VaultItemType.Card, card)]));

        Assert.Contains("Ada Lovelace", row);
        Assert.Contains("4111111111111111", row);
        Assert.Contains("12", row);
        Assert.Contains("2030", row);
        Assert.Contains("123", row);
        Assert.Contains("Visa", row);
    }

    [Fact]
    public void Export_WifiItem_IncludesWifiFieldsInTheClear()
    {
        var wifi = new WifiItem("Home", "MyNetwork", "correcthorsebatterystaple", WifiSecurityProtocol.Wpa3, IsHidden: true);
        string row = DataRow(_sut.Export([ItemOf(VaultItemType.WiFi, wifi)]));

        Assert.Contains("MyNetwork", row);
        Assert.Contains("correcthorsebatterystaple", row);
        Assert.Contains("Wpa3", row);
        Assert.Contains("true", row);
    }

    [Fact]
    public void Export_SecureNoteItem_IncludesNoteContentInTheClear()
    {
        var note = new SecureNoteItem("Passport", "Passport number: X1234567");
        string row = DataRow(_sut.Export([ItemOf(VaultItemType.Note, note)]));

        Assert.Contains("Passport number: X1234567", row);
    }

    [Fact]
    public void Export_LicenseItem_IncludesLicenseFieldsInTheClear()
    {
        var license = new LicenseItem("Office 365", "AAAAA-BBBBB-CCCCC-DDDDD", "Microsoft Office", DateTimeOffset.Parse("2027-01-01T00:00:00Z"));
        string row = DataRow(_sut.Export([ItemOf(VaultItemType.License, license)]));

        Assert.Contains("AAAAA-BBBBB-CCCCC-DDDDD", row);
        Assert.Contains("Microsoft Office", row);
        Assert.Contains("2027-01-01", row);
    }

    [Fact]
    public void Export_FieldContainingAComma_IsQuoted()
    {
        var note = new SecureNoteItem("Title, with comma", "content");
        string csv = _sut.Export([ItemOf(VaultItemType.Note, note)]);

        Assert.Contains("\"Title, with comma\"", csv);
    }

    [Fact]
    public void Export_FieldContainingAQuote_IsQuotedAndDoubled()
    {
        var note = new SecureNoteItem("Say \"hi\"", "content");
        string csv = _sut.Export([ItemOf(VaultItemType.Note, note)]);

        Assert.Contains("\"Say \"\"hi\"\"\"", csv);
    }
}
