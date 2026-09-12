namespace KeyKeeper.Core.Tools;

/// <summary>Generates random passwords for the "Şifre Üretici" tool and the generate-button on
/// <c>LoginFormPage</c>'s password field.</summary>
public interface IPasswordGenerator
{
    /// <summary>
    /// Produces a password matching <paramref name="options"/>.
    /// </summary>
    /// <exception cref="ArgumentException">No character category was selected, or
    /// <see cref="PasswordGeneratorOptions.Length"/> is too short to include at least one
    /// character from every selected category.</exception>
    string Generate(PasswordGeneratorOptions options);
}
