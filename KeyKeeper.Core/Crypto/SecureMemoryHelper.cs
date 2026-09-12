using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace KeyKeeper.Core.Crypto;

/// <summary>
/// Helpers for wiping sensitive buffers from memory as soon as they're no longer needed.
/// Callers are expected to pair these with try/finally around any buffer holding a
/// derived key, a decrypted secret, or a master password.
/// </summary>
public static class SecureMemoryHelper
{
    /// <summary>Zeroes a sensitive byte buffer in place. Safe to call with null/empty.</summary>
    public static void Clear(byte[]? buffer)
    {
        if (buffer is null || buffer.Length == 0)
        {
            return;
        }

        CryptographicOperations.ZeroMemory(buffer);
    }

    /// <summary>Zeroes a sensitive char buffer in place. Safe to call with null/empty.</summary>
    public static void Clear(char[]? buffer)
    {
        if (buffer is null || buffer.Length == 0)
        {
            return;
        }

        // Reinterpret as bytes so we get CryptographicOperations.ZeroMemory's guarantee
        // that the write isn't optimized away by the JIT (Array.Clear carries no such guarantee).
        CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(buffer.AsSpan()));
    }

    /// <summary>Zeroes multiple sensitive byte buffers in place. Safe to call with null entries.</summary>
    public static void Clear(params byte[]?[] buffers)
    {
        foreach (byte[]? buffer in buffers)
        {
            Clear(buffer);
        }
    }
}
