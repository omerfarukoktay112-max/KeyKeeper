using System.Security.Cryptography;
using System.Text;

namespace KeyKeeper.Core.Tools;

/// <summary>
/// <see cref="IPasswordGenerator"/> built on <see cref="RandomNumberGenerator"/> -- deliberately
/// not <see cref="Random"/>, which is not cryptographically secure and must never be used to
/// generate secrets in this app. Guarantees at least one character from every selected category
/// (rather than just pooling all selected characters and sampling uniformly, which could
/// legitimately produce a password missing a category the caller asked for) by drawing one
/// character per category first and filling the rest from the combined pool, then shuffling with
/// a <see cref="RandomNumberGenerator"/>-driven Fisher-Yates so the guaranteed characters aren't
/// always in the same leading positions.
/// </summary>
public sealed class PasswordGenerator : IPasswordGenerator
{
    private const string UppercaseChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string LowercaseChars = "abcdefghijklmnopqrstuvwxyz";
    private const string DigitChars = "0123456789";
    private const string SpecialChars = "!@#$%^&*()-_=+[]{};:,.<>?";

    /// <summary>Characters that look alike in many fonts, removed from every category when
    /// <see cref="PasswordGeneratorOptions.ExcludeAmbiguousCharacters"/> is set.</summary>
    private const string AmbiguousChars = "0O1lI";

    public string Generate(PasswordGeneratorOptions options)
    {
        List<string> categories = [];
        if (options.IncludeUppercase)
        {
            categories.Add(Strip(UppercaseChars, options));
        }

        if (options.IncludeLowercase)
        {
            categories.Add(Strip(LowercaseChars, options));
        }

        if (options.IncludeDigits)
        {
            categories.Add(Strip(DigitChars, options));
        }

        if (options.IncludeSpecialCharacters)
        {
            categories.Add(Strip(SpecialChars, options));
        }

        if (categories.Count == 0)
        {
            throw new ArgumentException("En az bir karakter kategorisi seçilmelidir.", nameof(options));
        }

        if (categories.Any(string.IsNullOrEmpty))
        {
            throw new ArgumentException("Seçilen kategorilerden biri, belirsiz karakterler hariç tutulduğunda boş kaldı.", nameof(options));
        }

        if (options.Length < categories.Count)
        {
            throw new ArgumentException($"Uzunluk, seçilen kategori sayısından ({categories.Count}) az olamaz.", nameof(options));
        }

        string combinedPool = string.Concat(categories);
        char[] password = new char[options.Length];

        int index = 0;
        foreach (string category in categories)
        {
            password[index++] = category[RandomNumberGenerator.GetInt32(category.Length)];
        }

        for (; index < password.Length; index++)
        {
            password[index] = combinedPool[RandomNumberGenerator.GetInt32(combinedPool.Length)];
        }

        Shuffle(password);
        return new string(password);
    }

    private static string Strip(string category, PasswordGeneratorOptions options)
    {
        if (!options.ExcludeAmbiguousCharacters)
        {
            return category;
        }

        StringBuilder builder = new(category.Length);
        foreach (char c in category)
        {
            if (!AmbiguousChars.Contains(c))
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    private static void Shuffle(char[] values)
    {
        for (int i = values.Length - 1; i > 0; i--)
        {
            int j = RandomNumberGenerator.GetInt32(i + 1);
            (values[i], values[j]) = (values[j], values[i]);
        }
    }
}
