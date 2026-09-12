using System.Security.Cryptography;
using System.Text;

namespace KeyKeeper.Core.Crypto;

/// <summary>
/// HMAC-SHA256-based <see cref="IMasterPasswordVerifier"/>.
///
/// The verifier is HMAC-SHA256(key: derivedKey, message: a fixed public label) — a one-way
/// function of the derived key. Storing this verifier does not let an attacker recover the
/// derived key (and therefore not the vault's data-encryption key either), unlike storing
/// the derived key itself or a plain hash of it that used the key as input rather than as
/// the HMAC key.
/// </summary>
public sealed class MasterPasswordVerifier : IMasterPasswordVerifier
{
    private static readonly byte[] VerifierLabel = Encoding.UTF8.GetBytes("KeyKeeper.MasterPasswordVerifier.v1");

    public byte[] CreateVerifier(byte[] derivedKey)
    {
        ArgumentNullException.ThrowIfNull(derivedKey);

        using var hmac = new HMACSHA256(derivedKey);
        return hmac.ComputeHash(VerifierLabel);
    }

    public bool Verify(byte[] derivedKey, byte[] storedVerifier)
    {
        ArgumentNullException.ThrowIfNull(derivedKey);
        ArgumentNullException.ThrowIfNull(storedVerifier);

        byte[] computedVerifier = CreateVerifier(derivedKey);
        try
        {
            // Fixed-size HMAC-SHA256 outputs, so a length mismatch (which FixedTimeEquals
            // rejects without a constant-time guarantee) only happens for a malformed/foreign
            // stored verifier, never as a function of how "close" a guessed password was.
            return CryptographicOperations.FixedTimeEquals(computedVerifier, storedVerifier);
        }
        finally
        {
            SecureMemoryHelper.Clear(computedVerifier);
        }
    }
}
