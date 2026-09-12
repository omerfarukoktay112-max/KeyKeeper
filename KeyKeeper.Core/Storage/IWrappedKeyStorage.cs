namespace KeyKeeper.Core.Storage;

/// <summary>
/// Persists the vault's database key for the biometric-unlock path, independently of the
/// master-password path.
///
/// The two unlock flows are independent ways to obtain the same 256-bit vault key:
/// - Master password flow: <c>IKeyDerivationService.DeriveKey</c> re-derives the key from the
///   password + stored salt every time. Nothing is ever persisted for this path.
/// - Biometric flow: the key is instead fetched from here, from OS-backed secure storage
///   (Android Keystore / iOS Keychain). A successful fingerprint/face/passcode check is what
///   gates *reading* it back, not this interface itself — wiring an actual biometric prompt
///   (BiometricPrompt / LocalAuthentication) in front of <see cref="RetrieveAsync"/> is a
///   later phase; this is just the storage primitive that flow will use.
///
/// "Wrapped" reflects that the underlying platform secure store itself encrypts the value at
/// rest (Keystore-backed on Android, Keychain on iOS) — this type does not additionally
/// AES-wrap the key before handing it to that store.
/// </summary>
public interface IWrappedKeyStorage
{
    /// <summary>Stores (overwriting any previous value) the vault key for biometric unlock.</summary>
    Task StoreAsync(byte[] key);

    /// <summary>Returns the stored key, or null if none has been stored (or it was removed).</summary>
    Task<byte[]?> RetrieveAsync();

    /// <summary>
    /// Removes the stored key (e.g. the user disables biometric unlock, or resets the vault).
    /// </summary>
    Task RemoveAsync();
}
