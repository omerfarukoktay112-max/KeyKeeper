using KeyKeeper.Core.Storage;

namespace KeyKeeper.Core.Tests.Storage;

public class WrappedKeyStorageTests
{
    /// <summary>
    /// Stands in for the real OS-backed store (Android Keystore/EncryptedSharedPreferences,
    /// iOS Keychain) so these tests verify WrappedKeyStorage's own wrapping/round-trip logic
    /// without needing a device -- the OS store's own security guarantees are Google's/Apple's
    /// tested responsibility, not something to re-prove here.
    /// </summary>
    private sealed class InMemorySecureKeyValueStore : ISecureKeyValueStore
    {
        private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

        public Task SetAsync(string key, string value)
        {
            _values[key] = value;
            return Task.CompletedTask;
        }

        public Task<string?> GetAsync(string key) =>
            Task.FromResult(_values.TryGetValue(key, out string? value) ? value : null);

        public void Remove(string key) => _values.Remove(key);
    }

    private static byte[] SampleKey() => Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();

    [Fact]
    public async Task StoreThenRetrieve_RoundTripsTheOriginalKeyBytes()
    {
        var sut = new WrappedKeyStorage(new InMemorySecureKeyValueStore());
        byte[] key = SampleKey();

        await sut.StoreAsync(key);
        byte[]? retrieved = await sut.RetrieveAsync();

        Assert.Equal(key, retrieved);
    }

    [Fact]
    public async Task Retrieve_NothingStoredYet_ReturnsNull()
    {
        var sut = new WrappedKeyStorage(new InMemorySecureKeyValueStore());

        Assert.Null(await sut.RetrieveAsync());
    }

    [Fact]
    public async Task Remove_ThenRetrieve_ReturnsNull()
    {
        var sut = new WrappedKeyStorage(new InMemorySecureKeyValueStore());
        await sut.StoreAsync(SampleKey());

        await sut.RemoveAsync();

        Assert.Null(await sut.RetrieveAsync());
    }

    [Fact]
    public async Task Remove_WhenNothingStored_DoesNotThrow()
    {
        var sut = new WrappedKeyStorage(new InMemorySecureKeyValueStore());

        Exception? exception = await Record.ExceptionAsync(() => sut.RemoveAsync());

        Assert.Null(exception);
    }

    [Fact]
    public async Task Store_CalledAgain_OverwritesThePreviousValue()
    {
        var sut = new WrappedKeyStorage(new InMemorySecureKeyValueStore());
        byte[] first = new byte[32];
        byte[] second = Enumerable.Repeat((byte)7, 32).ToArray();

        await sut.StoreAsync(first);
        await sut.StoreAsync(second);
        byte[]? retrieved = await sut.RetrieveAsync();

        Assert.Equal(second, retrieved);
    }
}
