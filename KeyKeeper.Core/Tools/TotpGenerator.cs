using OtpNet;

namespace KeyKeeper.Core.Tools;

/// <summary>
/// <see cref="ITotpGenerator"/> built on <c>Otp.NET</c>'s <see cref="Totp"/>, using
/// <see cref="TimeProvider"/> (not <see cref="DateTime.UtcNow"/> directly) for its time source --
/// same pattern as <see cref="Auth.InactivityTimerService"/> -- so its RFC 6238 test vectors can
/// be verified against a fixed instant instead of a real-time dependency.
///
/// Uses the standard 30-second step, SHA-1, 6-digit configuration: the values every mainstream
/// authenticator app (and every issuer's default otpauth:// URI) assumes when no algorithm is
/// specified, and what this app's <see cref="Models.LoginItem.TotpSecret"/> field is documented
/// to hold.
/// </summary>
public sealed class TotpGenerator : ITotpGenerator
{
    private const int PeriodSeconds = 30;

    private readonly TimeProvider _timeProvider;

    public TotpGenerator(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public TotpCode GenerateCode(string base32Secret)
    {
        if (string.IsNullOrWhiteSpace(base32Secret))
        {
            throw new ArgumentException("TOTP anahtarı boş olamaz.", nameof(base32Secret));
        }

        byte[] secretBytes;
        try
        {
            secretBytes = Base32Encoding.ToBytes(base32Secret);
        }
        catch (ArgumentException ex)
        {
            throw new ArgumentException("TOTP anahtarı geçerli bir Base32 değeri değil.", nameof(base32Secret), ex);
        }

        Totp totp = new(secretBytes, step: PeriodSeconds);
        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        string code = totp.ComputeTotp(now);
        int remainingSeconds = totp.RemainingSeconds(now);
        return new TotpCode(code, remainingSeconds, PeriodSeconds);
    }
}
