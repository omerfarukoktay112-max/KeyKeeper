using KeyKeeper.Core.Tools;

namespace KeyKeeper.Core.Tests.Tools;

public class PasswordGeneratorTests
{
    private readonly PasswordGenerator _sut = new();

    [Theory]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(64)]
    public void Generate_ReturnsPasswordOfRequestedLength(int length)
    {
        string password = _sut.Generate(new PasswordGeneratorOptions { Length = length });

        Assert.Equal(length, password.Length);
    }

    [Fact]
    public void Generate_TwoCalls_ProduceDifferentPasswords()
    {
        string first = _sut.Generate(new PasswordGeneratorOptions { Length = 20 });
        string second = _sut.Generate(new PasswordGeneratorOptions { Length = 20 });

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Generate_OnlyUppercaseSelected_ContainsOnlyUppercase()
    {
        string password = _sut.Generate(new PasswordGeneratorOptions
        {
            Length = 20,
            IncludeUppercase = true,
            IncludeLowercase = false,
            IncludeDigits = false,
            IncludeSpecialCharacters = false,
        });

        Assert.All(password, c => Assert.True(char.IsUpper(c)));
    }

    [Fact]
    public void Generate_AllCategoriesSelected_ContainsAtLeastOneFromEachCategory()
    {
        // Long enough that "at least one from each category, every run" is not a fluke: with all
        // four categories active the chance of a 40-char password missing one entirely is
        // astronomically small if the guarantee wasn't actually being enforced.
        string password = _sut.Generate(new PasswordGeneratorOptions
        {
            Length = 40,
            IncludeUppercase = true,
            IncludeLowercase = true,
            IncludeDigits = true,
            IncludeSpecialCharacters = true,
        });

        Assert.Contains(password, char.IsUpper);
        Assert.Contains(password, char.IsLower);
        Assert.Contains(password, char.IsDigit);
        Assert.Contains(password, c => !char.IsLetterOrDigit(c));
    }

    [Fact]
    public void Generate_ExcludeAmbiguousCharacters_NeverIncludesThem()
    {
        for (int i = 0; i < 50; i++)
        {
            string password = _sut.Generate(new PasswordGeneratorOptions
            {
                Length = 30,
                ExcludeAmbiguousCharacters = true,
            });

            Assert.DoesNotContain(password, c => "0O1lI".Contains(c));
        }
    }

    [Fact]
    public void Generate_NoCategorySelected_Throws()
    {
        var options = new PasswordGeneratorOptions
        {
            Length = 10,
            IncludeUppercase = false,
            IncludeLowercase = false,
            IncludeDigits = false,
            IncludeSpecialCharacters = false,
        };

        Assert.Throws<ArgumentException>(() => _sut.Generate(options));
    }

    [Fact]
    public void Generate_LengthShorterThanSelectedCategoryCount_Throws()
    {
        var options = new PasswordGeneratorOptions
        {
            Length = 2,
            IncludeUppercase = true,
            IncludeLowercase = true,
            IncludeDigits = true,
            IncludeSpecialCharacters = true,
        };

        Assert.Throws<ArgumentException>(() => _sut.Generate(options));
    }
}
