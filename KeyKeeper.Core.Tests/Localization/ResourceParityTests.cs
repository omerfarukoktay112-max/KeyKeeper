using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace KeyKeeper.Core.Tests.Localization;

/// <summary>
/// Guards the English (neutral) / Turkish (<c>.tr</c>) resource pairs against drift: every key must
/// exist in both files with a non-empty value, and every <c>{n}</c> placeholder must survive
/// translation. Reads the .resx sources straight from the repo so it also covers the MAUI app's
/// <c>AppStrings</c>, which this test project can't reference.
/// </summary>
public sealed class ResourceParityTests
{
    private static string RepoRoot([CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", ".."));

    private static Dictionary<string, string> Load(string relativePath) =>
        XDocument.Load(Path.Combine(RepoRoot(), relativePath)).Root!
            .Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string)d.Element("value")!);

    public static TheoryData<string, string> ResourcePairs => new()
    {
        { "KeyKeeper/Resources/Strings/AppStrings.resx", "KeyKeeper/Resources/Strings/AppStrings.tr.resx" },
        { "KeyKeeper.Core/Resources/CoreStrings.resx", "KeyKeeper.Core/Resources/CoreStrings.tr.resx" },
    };

    [Theory]
    [MemberData(nameof(ResourcePairs))]
    public void EnglishAndTurkishDefineTheSameKeys(string neutral, string turkish)
    {
        Dictionary<string, string> en = Load(neutral);
        Dictionary<string, string> tr = Load(turkish);

        Assert.Empty(en.Keys.Except(tr.Keys));
        Assert.Empty(tr.Keys.Except(en.Keys));
        Assert.All(en.Values, v => Assert.False(string.IsNullOrWhiteSpace(v)));
        Assert.All(tr.Values, v => Assert.False(string.IsNullOrWhiteSpace(v)));
    }

    [Theory]
    [MemberData(nameof(ResourcePairs))]
    public void TranslationsKeepTheSamePlaceholders(string neutral, string turkish)
    {
        Dictionary<string, string> en = Load(neutral);
        Dictionary<string, string> tr = Load(turkish);

        foreach ((string key, string english) in en)
        {
            Assert.True(
                Placeholders(english).SequenceEqual(Placeholders(tr[key])),
                $"Placeholders of '{key}' differ between English and Turkish.");
        }
    }

    private static IEnumerable<string> Placeholders(string text) =>
        System.Text.RegularExpressions.Regex.Matches(text, @"\{\d+\}").Select(m => m.Value).Order();
}
