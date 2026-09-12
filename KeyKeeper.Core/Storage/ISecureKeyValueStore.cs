namespace KeyKeeper.Core.Storage;

/// <summary>
/// A minimal string key-value store backed by the platform's secure storage (Android
/// Keystore-backed EncryptedSharedPreferences / iOS Keychain via
/// <c>Microsoft.Maui.Storage.SecureStorage</c> in the MAUI head project). Exists purely as a
/// seam so <see cref="WrappedKeyStorage"/> stays testable on desktop without a real
/// Keystore/Keychain — see <c>KeyKeeper.Services.MauiSecureKeyValueStore</c> for the real
/// implementation.
/// </summary>
public interface ISecureKeyValueStore
{
    Task SetAsync(string key, string value);

    /// <summary>Returns null if nothing is stored under <paramref name="key"/>.</summary>
    Task<string?> GetAsync(string key);

    /// <summary>Removes the entry, if present. A no-op if it isn't.</summary>
    void Remove(string key);
}
