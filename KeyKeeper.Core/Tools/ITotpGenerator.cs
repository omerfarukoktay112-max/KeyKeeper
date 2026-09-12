namespace KeyKeeper.Core.Tools;

/// <summary>Generates TOTP (RFC 6238) codes from a <see cref="Models.LoginItem.TotpSecret"/> for
/// the live code + countdown shown on a login item's detail page.</summary>
public interface ITotpGenerator
{
    /// <summary>
    /// Computes the current TOTP code for <paramref name="base32Secret"/>.
    /// </summary>
    /// <param name="base32Secret">The shared secret, Base32-encoded (the format issuers normally
    /// hand out, e.g. in an otpauth:// URI or QR code).</param>
    /// <exception cref="ArgumentException"><paramref name="base32Secret"/> is null, empty, or not
    /// valid Base32.</exception>
    TotpCode GenerateCode(string base32Secret);
}
