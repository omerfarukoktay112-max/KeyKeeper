namespace KeyKeeper.Core.Tools;

/// <summary>
/// Inputs to <see cref="IPasswordGenerator.Generate"/>. At least one <c>Include*</c> flag must be
/// <c>true</c> and <see cref="Length"/> must be at least the number of flags that are set (one
/// character from each selected category is guaranteed) -- see
/// <see cref="PasswordGenerator"/>'s doc comment for why this throws rather than silently
/// defaulting.
/// </summary>
public sealed record PasswordGeneratorOptions
{
    public int Length { get; init; } = 16;

    public bool IncludeUppercase { get; init; } = true;

    public bool IncludeLowercase { get; init; } = true;

    public bool IncludeDigits { get; init; } = true;

    public bool IncludeSpecialCharacters { get; init; } = true;

    /// <summary>Excludes characters that look alike in most fonts (<c>0</c>/<c>O</c>,
    /// <c>1</c>/<c>l</c>/<c>I</c>, ...) -- useful for passwords a person will have to type by hand
    /// from a screen rather than paste.</summary>
    public bool ExcludeAmbiguousCharacters { get; init; }
}
