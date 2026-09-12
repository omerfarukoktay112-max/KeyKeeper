using Zxcvbn;

namespace KeyKeeper.Core.Tools;

/// <summary>
/// <see cref="IPasswordStrengthAnalyzer"/> built on the <c>zxcvbn-core</c> package -- a .NET port
/// of Dropbox's zxcvbn estimator -- rather than a hand-rolled set of heuristics. zxcvbn already
/// detects exactly the weakness patterns this tool needs (repeated characters, sequential
/// keyboard runs like "qwerty"/"12345", dates, and dictionary/l33t-substitution words) which a
/// from-scratch implementation would have to reinvent, has no dependencies of its own, and runs
/// entirely offline against its own bundled dictionaries -- no network call, consistent with this
/// app's zero-knowledge requirement. See <c>KeyKeeper.Core.csproj</c> for the package reference and
/// the reasoning for where it lives.
///
/// zxcvbn's own <c>Result.Score</c> is 0-4; this app maps that directly onto
/// <see cref="PasswordStrengthLevel"/>'s four values (0→Weak, 1→Medium, 2→Strong, 3 and 4→VeryStrong)
/// since zxcvbn's top two scores ("safely unguessable" and "very unguessable") don't need a
/// separate bucket in this app's simpler UI.
///
/// zxcvbn-core's <see cref="Result"/> exposes <c>GuessesLog10</c> (log10 of the estimated guess
/// count) rather than a bits-of-entropy figure directly; <see cref="PasswordStrengthResult.Entropy"/>
/// converts that to bits (log2) since bits is the more familiar unit to show in this app's UI.
/// </summary>
public sealed class PasswordStrengthAnalyzer : IPasswordStrengthAnalyzer
{
    private static readonly double Log2Of10 = Math.Log(10, 2);

    public PasswordStrengthResult Analyze(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return new PasswordStrengthResult(PasswordStrengthLevel.Weak, 0, 0);
        }

        Result result = Zxcvbn.Core.EvaluatePassword(password);
        PasswordStrengthLevel level = result.Score switch
        {
            <= 0 => PasswordStrengthLevel.Weak,
            1 => PasswordStrengthLevel.Medium,
            2 => PasswordStrengthLevel.Strong,
            _ => PasswordStrengthLevel.VeryStrong,
        };

        double entropyBits = result.GuessesLog10 * Log2Of10;
        return new PasswordStrengthResult(level, result.Score, entropyBits);
    }
}
