namespace KeyKeeper.Core.Tools;

/// <summary>Analyzes a password's strength for the live indicator shown on the generator page and
/// on password fields in item forms. Runs entirely offline -- no breach-check APIs, consistent
/// with this app's zero-knowledge, offline-first design.</summary>
public interface IPasswordStrengthAnalyzer
{
    /// <summary>Returns <see cref="PasswordStrengthLevel.Weak"/> with zero score/entropy for an
    /// empty password rather than throwing -- callers analyze live as-you-type text, which starts
    /// out empty.</summary>
    PasswordStrengthResult Analyze(string password);
}
