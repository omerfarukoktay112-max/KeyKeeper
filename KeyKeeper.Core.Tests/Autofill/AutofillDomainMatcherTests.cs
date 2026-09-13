using KeyKeeper.Core.Autofill;

namespace KeyKeeper.Core.Tests.Autofill;

public class AutofillDomainMatcherTests
{
    [Theory]
    [InlineData("https://github.com", "github.com")]
    [InlineData("http://github.com", "github.com")]
    [InlineData("github.com", "github.com")]
    [InlineData("https://www.github.com", "github.com")]
    [InlineData("www.github.com", "github.com")]
    [InlineData("https://GitHub.com", "github.com")]
    [InlineData("https://github.com/login?next=/", "github.com")]
    [InlineData("https://github.com:8443/", "github.com")]
    public void NormalizeHost_StripsSchemeWwwCasePathPort(string input, string expected)
    {
        Assert.Equal(expected, AutofillDomainMatcher.NormalizeHost(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeHost_EmptyOrWhitespace_ReturnsNull(string? input)
    {
        Assert.Null(AutofillDomainMatcher.NormalizeHost(input));
    }

    [Fact]
    public void NormalizeHost_BareWww_ReturnsNull()
    {
        // "www." alone strips down to an empty host -- no usable domain, not "".
        Assert.Null(AutofillDomainMatcher.NormalizeHost("www."));
    }

    [Theory]
    [InlineData("https://github.com", "github.com")]
    [InlineData("https://github.com", "www.github.com")]
    [InlineData("https://www.github.com", "github.com")]
    [InlineData("http://github.com", "https://github.com")]
    [InlineData("github.com", "accounts.github.com")]
    [InlineData("https://google.com", "accounts.google.com")]
    [InlineData("https://google.com", "mail.accounts.google.com")]
    public void IsMatch_SameOrSubdomainOfSavedHost_ReturnsTrue(string savedUrl, string requestHost)
    {
        Assert.True(AutofillDomainMatcher.IsMatch(savedUrl, requestHost));
    }

    [Theory]
    [InlineData("https://accounts.google.com", "google.com")] // narrower saved scope doesn't broaden
    [InlineData("https://google.com", "evilgoogle.com")] // no label boundary -- must not match
    [InlineData("https://google.com", "google.com.evil.com")] // suffix trick -- must not match
    [InlineData("https://github.com", "gitlab.com")]
    [InlineData("https://github.com", "notgithub.com")]
    public void IsMatch_UnrelatedOrBroaderDomain_ReturnsFalse(string savedUrl, string requestHost)
    {
        Assert.False(AutofillDomainMatcher.IsMatch(savedUrl, requestHost));
    }

    [Theory]
    [InlineData(null, "github.com")]
    [InlineData("https://github.com", null)]
    [InlineData(null, null)]
    [InlineData("", "github.com")]
    public void IsMatch_MissingEitherSide_ReturnsFalse(string? savedUrl, string? requestHost)
    {
        Assert.False(AutofillDomainMatcher.IsMatch(savedUrl, requestHost));
    }
}
