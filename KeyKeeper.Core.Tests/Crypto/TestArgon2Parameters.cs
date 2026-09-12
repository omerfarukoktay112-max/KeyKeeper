using KeyKeeper.Core.Crypto;

namespace KeyKeeper.Core.Tests.Crypto;

/// <summary>
/// Deliberately cheap Argon2id parameters for fast unit tests. Never use these outside
/// tests — see <see cref="Argon2Constants"/> for the real, documented production defaults.
/// </summary>
internal static class TestArgon2Parameters
{
    public static readonly Argon2Parameters Fast = new(
        memorySizeKiB: 8 * 1024,
        iterations: 1,
        parallelism: 1);
}
