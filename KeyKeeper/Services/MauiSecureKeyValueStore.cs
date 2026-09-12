using KeyKeeper.Core.Storage;
using Microsoft.Maui.Storage;

namespace KeyKeeper.Services;

/// <summary>
/// <see cref="ISecureKeyValueStore"/> backed by <see cref="SecureStorage"/> — Android
/// Keystore-backed EncryptedSharedPreferences / iOS Keychain under the hood.
/// </summary>
public sealed class MauiSecureKeyValueStore : ISecureKeyValueStore
{
    public Task SetAsync(string key, string value) => SecureStorage.Default.SetAsync(key, value);

    public Task<string?> GetAsync(string key) => SecureStorage.Default.GetAsync(key);

    public void Remove(string key) => SecureStorage.Default.Remove(key);
}
