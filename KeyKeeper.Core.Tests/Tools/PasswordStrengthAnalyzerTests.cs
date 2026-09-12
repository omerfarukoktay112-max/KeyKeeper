using KeyKeeper.Core.Tools;

namespace KeyKeeper.Core.Tests.Tools;

public class PasswordStrengthAnalyzerTests
{
    private readonly PasswordStrengthAnalyzer _sut = new();

    [Fact]
    public void Analyze_EmptyPassword_ReturnsWeakWithZeroScore()
    {
        PasswordStrengthResult result = _sut.Analyze(string.Empty);

        Assert.Equal(PasswordStrengthLevel.Weak, result.Level);
        Assert.Equal(0, result.Score);
        Assert.Equal(0, result.Entropy);
    }

    [Theory]
    [InlineData("password")]
    [InlineData("123456")]
    [InlineData("qwerty")]
    [InlineData("aaaaaaaa")]
    public void Analyze_KnownWeakPasswords_ClassifiedAsWeak(string password)
    {
        PasswordStrengthResult result = _sut.Analyze(password);

        Assert.Equal(PasswordStrengthLevel.Weak, result.Level);
    }

    [Theory]
    [InlineData("xQ7#mK9$pL2@vN5!wR8&")]
    [InlineData("correct-horse-battery-staple-42!")]
    public void Analyze_KnownStrongPasswords_ClassifiedAsStrongOrHigher(string password)
    {
        PasswordStrengthResult result = _sut.Analyze(password);

        Assert.True(result.Level >= PasswordStrengthLevel.Strong,
            $"Expected {password} to be Strong or VeryStrong but was {result.Level} (score {result.Score}).");
    }

    [Fact]
    public void Analyze_StrongerPassword_HasHigherEntropyThanWeakerOne()
    {
        PasswordStrengthResult weak = _sut.Analyze("password");
        PasswordStrengthResult strong = _sut.Analyze("xQ7#mK9$pL2@vN5!wR8&");

        Assert.True(strong.Entropy > weak.Entropy);
    }
}
