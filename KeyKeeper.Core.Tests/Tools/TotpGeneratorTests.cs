using KeyKeeper.Core.Tools;
using Microsoft.Extensions.Time.Testing;
using OtpNet;

namespace KeyKeeper.Core.Tests.Tools;

/// <summary>
/// Verified against RFC 6238 Appendix B's official test vectors (SHA-1, 8-digit, ASCII secret
/// "12345678901234567890"). Those vectors are published as 8-digit codes; a 6-digit code is
/// always just an 8-digit code's last 6 digits, since (guess mod 10^8) mod 10^6 == guess mod 10^6
/// -- so the expected codes below are the RFC vectors truncated to 6 digits, matching this app's
/// (and Otp.NET's default) 6-digit TOTP size. The Base32 secret itself is derived from the RFC's
/// raw ASCII bytes via Otp.NET's own <see cref="Base32Encoding"/> at test time rather than
/// hand-transcribed, to rule out a transcription error in the test itself.
/// </summary>
public class TotpGeneratorTests
{
    private static readonly string Secret = Base32Encoding.ToString(System.Text.Encoding.ASCII.GetBytes("12345678901234567890"));

    [Theory]
    [InlineData(59L, "287082")]
    [InlineData(1111111109L, "081804")]
    [InlineData(1111111111L, "050471")]
    [InlineData(1234567890L, "005924")]
    [InlineData(2000000000L, "279037")]
    public void GenerateCode_KnownRfc6238Vector_ProducesExpectedCode(long unixTimeSeconds, string expectedCode)
    {
        var time = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        time.SetUtcNow(DateTimeOffset.FromUnixTimeSeconds(unixTimeSeconds));
        var sut = new TotpGenerator(time);

        TotpCode result = sut.GenerateCode(Secret);

        Assert.Equal(expectedCode, result.Code);
    }

    [Fact]
    public void GenerateCode_ReportsPeriodOfThirtySeconds()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        time.SetUtcNow(DateTimeOffset.FromUnixTimeSeconds(59));
        var sut = new TotpGenerator(time);

        TotpCode result = sut.GenerateCode(Secret);

        Assert.Equal(30, result.PeriodSeconds);
    }

    [Fact]
    public void GenerateCode_AtSecond59_HasOneSecondRemainingInWindow()
    {
        // Second 59 falls in the [30, 60) step, one second before it rolls over.
        var time = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        time.SetUtcNow(DateTimeOffset.FromUnixTimeSeconds(59));
        var sut = new TotpGenerator(time);

        TotpCode result = sut.GenerateCode(Secret);

        Assert.Equal(1, result.RemainingSeconds);
    }

    [Fact]
    public void GenerateCode_SameSecretDifferentTimes_ProducesDifferentCodes()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var sut = new TotpGenerator(time);

        time.SetUtcNow(DateTimeOffset.FromUnixTimeSeconds(59));
        string first = sut.GenerateCode(Secret).Code;

        time.SetUtcNow(DateTimeOffset.FromUnixTimeSeconds(1111111109));
        string second = sut.GenerateCode(Secret).Code;

        Assert.NotEqual(first, second);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GenerateCode_NullOrWhitespaceSecret_Throws(string? secret)
    {
        var sut = new TotpGenerator(new FakeTimeProvider());

        Assert.Throws<ArgumentException>(() => sut.GenerateCode(secret!));
    }

    [Fact]
    public void GenerateCode_InvalidBase32Secret_Throws()
    {
        var sut = new TotpGenerator(new FakeTimeProvider());

        Assert.Throws<ArgumentException>(() => sut.GenerateCode("not-valid-base32!!!"));
    }
}
