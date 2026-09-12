namespace KeyKeeper.Core.Tools;

/// <summary>Result of <see cref="IPasswordStrengthAnalyzer.Analyze"/>.</summary>
/// <param name="Level">This app's own 4-tier classification (see <see cref="PasswordStrengthLevel"/>).</param>
/// <param name="Score">The underlying zxcvbn score, 0 (weakest) to 4 (strongest) -- kept alongside
/// <paramref name="Level"/> so UI can drive a finer-grained strength bar than 4 buckets allow.</param>
/// <param name="Entropy">Estimated entropy in bits, as computed by zxcvbn's pattern analysis
/// (not a naive charset-size-and-length calculation).</param>
public sealed record PasswordStrengthResult(PasswordStrengthLevel Level, int Score, double Entropy);
