namespace KeyKeeper.Core.Tools;

/// <summary>This app's own 4-tier strength scale, shown to the user in Turkish (Zayıf/Orta/Güçlü/
/// Çok Güçlü). See <see cref="PasswordStrengthAnalyzer"/>'s doc comment for how zxcvbn's 0-4
/// score maps onto this.</summary>
public enum PasswordStrengthLevel
{
    Weak = 0,
    Medium = 1,
    Strong = 2,
    VeryStrong = 3,
}
