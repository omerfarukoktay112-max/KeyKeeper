namespace KeyKeeper.Core.Storage;

/// <summary>
/// <see cref="IWrappedKeyStorage"/> built on <see cref="ISecureKeyValueStore"/>.
/// </summary>
public sealed class WrappedKeyStorage : IWrappedKeyStorage
{
    // Lookup name for the entry inside the platform secure store. Not a cryptographic secret,
    // just an identifier -- the store itself is what protects the value.
    private const string StorageKey = "KeyKeeper.WrappedVaultKey";

    private readonly ISecureKeyValueStore _store;

    public WrappedKeyStorage(ISecureKeyValueStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public Task StoreAsync(byte[] key)
    {
        ArgumentNullException.ThrowIfNull(key);

        // SecureStorage's platform API is string-based, so a base64 string is an unavoidable
        // intermediate at this boundary -- the OS-level store (Keystore/Keychain) is what
        // actually encrypts it at rest, same as any other value passed through that API.
        string encoded = Convert.ToBase64String(key);
        return _store.SetAsync(StorageKey, encoded);
    }

    public async Task<byte[]?> RetrieveAsync()
    {
        string? encoded = await _store.GetAsync(StorageKey).ConfigureAwait(false);
        return string.IsNullOrEmpty(encoded) ? null : Convert.FromBase64String(encoded);
    }

    public Task RemoveAsync()
    {
        _store.Remove(StorageKey);
        return Task.CompletedTask;
    }
}
