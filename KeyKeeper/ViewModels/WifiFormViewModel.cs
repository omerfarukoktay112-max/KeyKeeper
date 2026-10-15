using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeyKeeper.Core.Data;
using KeyKeeper.Core.Models;
using KeyKeeper.Core.Tools;
using Microsoft.Maui.Graphics;
using KeyKeeper.Localization;

namespace KeyKeeper.ViewModels;

public sealed partial class WifiFormViewModel : VaultItemFormViewModelBase<WifiItem>
{
    /// <summary>Display order for the <c>SecurityProtocol</c> <c>Picker</c> in
    /// <c>WifiFormPage.xaml</c> — index N here must stay <see cref="WifiSecurityProtocol"/>'s
    /// underlying value N, since <see cref="SecurityProtocolIndex"/> casts directly between them.</summary>
    public static readonly string[] SecurityProtocolOptions = [S.WifiOpen, "WEP", "WPA2", "WPA3"];

    private readonly IPasswordStrengthAnalyzer _strengthAnalyzer;

    public WifiFormViewModel(IVaultItemRepository repository, IFolderRepository folders, IClipboardClearingService clipboardClearingService, IPasswordStrengthAnalyzer strengthAnalyzer)
        : base(repository, folders, clipboardClearingService)
    {
        _strengthAnalyzer = strengthAnalyzer;
    }

    [ObservableProperty]
    private string title = string.Empty;

    [ObservableProperty]
    private string ssid = string.Empty;

    [ObservableProperty]
    private string? password;

    [ObservableProperty]
    private int securityProtocolIndex = (int)WifiSecurityProtocol.Wpa2;

    [ObservableProperty]
    private bool isHidden;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPasswordMasked))]
    private bool isPasswordVisible;

    public bool IsPasswordMasked => !IsPasswordVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPasswordStrength))]
    [NotifyPropertyChangedFor(nameof(PasswordStrengthLabel))]
    [NotifyPropertyChangedFor(nameof(PasswordStrengthColor))]
    [NotifyPropertyChangedFor(nameof(PasswordStrengthFraction))]
    private PasswordStrengthResult? passwordStrength;

    public bool HasPasswordStrength => PasswordStrength is not null;

    public string PasswordStrengthLabel => PasswordStrength is { } s ? PasswordStrengthPresentation.Label(s.Level) : string.Empty;

    public Color PasswordStrengthColor => PasswordStrength is { } s ? PasswordStrengthPresentation.Color(s.Level) : Colors.Transparent;

    public double PasswordStrengthFraction => PasswordStrength is { } s ? PasswordStrengthPresentation.Fraction(s.Level) : 0;

    partial void OnPasswordChanged(string? value) => PasswordStrength = string.IsNullOrEmpty(value) ? null : _strengthAnalyzer.Analyze(value);

    [RelayCommand]
    private void TogglePasswordVisibility() => IsPasswordVisible = !IsPasswordVisible;

    protected override WifiItem BuildPayload() =>
        new(Title, Ssid, Password, (WifiSecurityProtocol)SecurityProtocolIndex, IsHidden);

    protected override void LoadFromPayload(WifiItem payload)
    {
        Title = payload.Title;
        Ssid = payload.Ssid;
        Password = payload.Password;
        SecurityProtocolIndex = (int)payload.SecurityProtocol;
        IsHidden = payload.IsHidden;
    }
}
