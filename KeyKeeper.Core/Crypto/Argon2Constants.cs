namespace KeyKeeper.Core.Crypto;

/// <summary>
/// Default Argon2id parameters and salt sizing for master-password key derivation.
///
/// The defaults below are the "m=19456 (19 MiB), t=2, p=1" row from the OWASP Password
/// Storage Cheat Sheet's Argon2id configuration table
/// (https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html),
/// chosen over the higher-memory rows (e.g. 46 MiB) as a starting point for mobile devices:
/// it keeps peak memory usage low enough to avoid pressure on low-end/older phones while
/// still meeting OWASP's minimum security bar for Argon2id.
///
/// These are intentionally NOT hard-coded into the services that use them — every call site
/// takes an explicit <see cref="Argon2Parameters"/> instance. Once we have real device
/// benchmarks (Phase 3+), calibrate per-device (or per OS/RAM tier) and adjust these defaults,
/// or have the app pick a tier at first run based on a measured KDF timing.
/// </summary>
public static class Argon2Constants
{
    /// <summary>Argon2id memory cost in KiB (19 MiB). OWASP Argon2id row: m=19456, t=2, p=1.</summary>
    public const int DefaultMemorySizeKiB = 19_456;

    /// <summary>Argon2id time cost (number of passes).</summary>
    public const int DefaultIterations = 2;

    /// <summary>Argon2id degree of parallelism (lanes/threads).</summary>
    public const int DefaultParallelism = 1;

    /// <summary>Derived key length in bytes (256-bit key).</summary>
    public const int DefaultOutputLengthBytes = 32;

    /// <summary>
    /// Minimum acceptable salt size in bytes. 16 bytes (128-bit) is the generally
    /// recommended minimum for Argon2 salts.
    /// </summary>
    public const int MinSaltSizeBytes = 16;
}
