using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace KeyKeeper.Core.Crypto;

/// <summary>
/// Argon2id-based <see cref="IKeyDerivationService"/> implementation, backed by
/// Konscious.Security.Cryptography.Argon2.
/// </summary>
public sealed class Argon2idKeyDerivationService : IKeyDerivationService
{
    public byte[] DeriveKey(char[] masterPassword, byte[] salt, Argon2Parameters parameters)
    {
        ArgumentNullException.ThrowIfNull(masterPassword);
        ArgumentNullException.ThrowIfNull(salt);
        ArgumentNullException.ThrowIfNull(parameters);

        if (masterPassword.Length == 0)
        {
            throw new ArgumentException("Master password must not be empty.", nameof(masterPassword));
        }

        if (salt.Length < Argon2Constants.MinSaltSizeBytes)
        {
            throw new ArgumentException(
                $"Salt must be at least {Argon2Constants.MinSaltSizeBytes} bytes.", nameof(salt));
        }

        byte[] passwordBytes = Encoding.UTF8.GetBytes(masterPassword);
        try
        {
            using var argon2 = new Argon2id(passwordBytes)
            {
                Salt = salt,
                MemorySize = parameters.MemorySizeKiB,
                Iterations = parameters.Iterations,
                DegreeOfParallelism = parameters.Parallelism,
            };

            return argon2.GetBytes(parameters.OutputLengthBytes);
        }
        finally
        {
            SecureMemoryHelper.Clear(passwordBytes);
        }
    }

    public byte[] GenerateSalt(int sizeBytes = Argon2Constants.MinSaltSizeBytes)
    {
        if (sizeBytes < Argon2Constants.MinSaltSizeBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sizeBytes), $"Salt must be at least {Argon2Constants.MinSaltSizeBytes} bytes.");
        }

        return RandomNumberGenerator.GetBytes(sizeBytes);
    }
}
