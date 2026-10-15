using KeyKeeper.Core.Tools;
using Microsoft.Maui.Graphics;
using KeyKeeper.Localization;

namespace KeyKeeper.ViewModels;

/// <summary>Turkish label, colour, and fill-fraction for each <see cref="PasswordStrengthLevel"/>
/// — shared by every ViewModel with a live strength indicator (<see cref="LoginFormViewModel"/>,
/// <see cref="WifiFormViewModel"/>, <see cref="PasswordGeneratorViewModel"/>) so the mapping is
/// defined once instead of duplicated per form.</summary>
public static class PasswordStrengthPresentation
{
    public static string Label(PasswordStrengthLevel level) => level switch
    {
        PasswordStrengthLevel.Weak => S.StrengthWeak,
        PasswordStrengthLevel.Medium => S.StrengthMedium,
        PasswordStrengthLevel.Strong => S.StrengthStrong,
        PasswordStrengthLevel.VeryStrong => S.StrengthVeryStrong,
        _ => string.Empty,
    };

    public static Color Color(PasswordStrengthLevel level) => level switch
    {
        PasswordStrengthLevel.Weak => Colors.Red,
        PasswordStrengthLevel.Medium => Colors.Orange,
        PasswordStrengthLevel.Strong => Colors.YellowGreen,
        PasswordStrengthLevel.VeryStrong => Colors.Green,
        _ => Colors.Gray,
    };

    /// <summary>0-1 fill fraction for a <c>ProgressBar</c>, derived from the 4-value enum.</summary>
    public static double Fraction(PasswordStrengthLevel level) => ((int)level + 1) / 4.0;
}
