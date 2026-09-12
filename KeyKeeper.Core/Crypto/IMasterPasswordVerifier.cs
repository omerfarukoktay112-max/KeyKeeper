namespace KeyKeeper.Core.Crypto;

/// <summary>
/// Produces and checks a storable "verifier" that confirms a master password was entered
/// correctly, without ever storing (or being able to reconstruct) the master password or
/// the vault's derived encryption key from the verifier alone.
///
/// Usage: derive a key from the master password with <see cref="IKeyDerivationService"/>,
/// then pass that key here — never the raw password.
/// </summary>
public interface IMasterPasswordVerifier
{
    /// <summary>
    /// Creates a verifier from a key already derived from the master password
    /// (via <see cref="IKeyDerivationService.DeriveKey"/>). Safe to persist.
    /// </summary>
    byte[] CreateVerifier(byte[] derivedKey);

    /// <summary>
    /// Checks whether <paramref name="derivedKey"/> (derived from a just-entered master
    /// password) matches <paramref name="storedVerifier"/>, using a constant-time comparison
    /// so the result can't leak timing information about how close the guess was.
    /// </summary>
    bool Verify(byte[] derivedKey, byte[] storedVerifier);
}
