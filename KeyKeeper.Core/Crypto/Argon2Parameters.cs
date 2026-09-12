namespace KeyKeeper.Core.Crypto;

/// <summary>
/// Configurable Argon2id cost parameters. See <see cref="Argon2Constants"/> for the
/// documented default values and the reasoning behind them.
/// </summary>
public sealed record Argon2Parameters
{
    /// <summary>Memory cost in KiB.</summary>
    public int MemorySizeKiB { get; }

    /// <summary>Time cost (number of passes).</summary>
    public int Iterations { get; }

    /// <summary>Degree of parallelism (lanes/threads).</summary>
    public int Parallelism { get; }

    /// <summary>Derived key length in bytes.</summary>
    public int OutputLengthBytes { get; }

    public Argon2Parameters(
        int memorySizeKiB,
        int iterations,
        int parallelism,
        int outputLengthBytes = Argon2Constants.DefaultOutputLengthBytes)
    {
        if (memorySizeKiB <= 0)
            throw new ArgumentOutOfRangeException(nameof(memorySizeKiB), "Memory size must be positive.");
        if (iterations <= 0)
            throw new ArgumentOutOfRangeException(nameof(iterations), "Iterations must be positive.");
        if (parallelism <= 0)
            throw new ArgumentOutOfRangeException(nameof(parallelism), "Parallelism must be positive.");
        if (outputLengthBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(outputLengthBytes), "Output length must be positive.");

        MemorySizeKiB = memorySizeKiB;
        Iterations = iterations;
        Parallelism = parallelism;
        OutputLengthBytes = outputLengthBytes;
    }

    /// <summary>The documented mobile-friendly default (see <see cref="Argon2Constants"/>).</summary>
    public static Argon2Parameters Default { get; } = new(
        Argon2Constants.DefaultMemorySizeKiB,
        Argon2Constants.DefaultIterations,
        Argon2Constants.DefaultParallelism,
        Argon2Constants.DefaultOutputLengthBytes);
}
