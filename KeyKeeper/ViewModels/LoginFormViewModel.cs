using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeyKeeper.Core.Data;
using KeyKeeper.Core.Models;
using KeyKeeper.Core.Tools;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Graphics;

namespace KeyKeeper.ViewModels;

public sealed partial class LoginFormViewModel : VaultItemFormViewModelBase<LoginItem>
{
    private readonly IPasswordStrengthAnalyzer _strengthAnalyzer;
    private readonly IPasswordGenerator _passwordGenerator;
    private readonly ITotpGenerator _totpGenerator;

    private IDispatcherTimer? _totpTimer;

    public LoginFormViewModel(
        IVaultItemRepository repository,
        IFolderRepository folders,
        IClipboardClearingService clipboardClearingService,
        IPasswordStrengthAnalyzer strengthAnalyzer,
        IPasswordGenerator passwordGenerator,
        ITotpGenerator totpGenerator)
        : base(repository, folders, clipboardClearingService)
    {
        _strengthAnalyzer = strengthAnalyzer;
        _passwordGenerator = passwordGenerator;
        _totpGenerator = totpGenerator;
    }

    [ObservableProperty]
    private string title = string.Empty;

    [ObservableProperty]
    private string? username;

    [ObservableProperty]
    private string? password;

    [ObservableProperty]
    private string? url;

    [ObservableProperty]
    private string? totpSecret;

    /// <summary>Bound to the password Entry's <c>IsPassword</c> — masked by default, same as the
    /// master-password fields elsewhere in this app, so this isn't a second masking mechanism.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPasswordMasked))]
    private bool isPasswordVisible;

    public bool IsPasswordMasked => !IsPasswordVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTotpSecretMasked))]
    private bool isTotpSecretVisible;

    public bool IsTotpSecretMasked => !IsTotpSecretVisible;

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

    /// <summary>Live TOTP code shown on this form when <see cref="TotpSecret"/> is set — null
    /// while there's no secret, or while it's invalid Base32 (see <see cref="TotpError"/>).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTotpCode))]
    private string? totpDisplayCode;

    public bool HasTotpCode => !string.IsNullOrEmpty(TotpDisplayCode);

    [ObservableProperty]
    private int totpRemainingSeconds;

    [ObservableProperty]
    private int totpPeriodSeconds = 30;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTotpError))]
    private string? totpError;

    public bool HasTotpError => !string.IsNullOrEmpty(TotpError);

    [RelayCommand]
    private void TogglePasswordVisibility() => IsPasswordVisible = !IsPasswordVisible;

    [RelayCommand]
    private void ToggleTotpSecretVisibility() => IsTotpSecretVisible = !IsTotpSecretVisible;

    /// <summary>Bound to the "Üret" button next to the password field -- fills the field with a
    /// freshly generated strong password (16 characters, every category) rather than opening the
    /// standalone generator page, so the common case doesn't need a page round-trip.</summary>
    [RelayCommand]
    private void GeneratePassword()
    {
        Password = _passwordGenerator.Generate(new PasswordGeneratorOptions { Length = 16 });
        IsPasswordVisible = true;
    }

    partial void OnTotpSecretChanged(string? value) => RefreshTotpTimer();

    /// <summary>Starts (or stops) the 1-second TOTP refresh timer based on whether there's a
    /// secret to show a code for. Called once <see cref="Initialize"/> has loaded
    /// <see cref="TotpSecret"/> in edit mode, and again on every edit of the field so typing a new
    /// secret updates the live code immediately.</summary>
    private void RefreshTotpTimer()
    {
        if (string.IsNullOrWhiteSpace(TotpSecret))
        {
            _totpTimer?.Stop();
            TotpDisplayCode = null;
            TotpError = null;
            return;
        }

        UpdateTotpCode();
        _totpTimer ??= CreateTotpTimer();
        _totpTimer.Start();
    }

    private IDispatcherTimer CreateTotpTimer()
    {
        IDispatcherTimer timer = Application.Current!.Dispatcher.CreateTimer();
        timer.Interval = TimeSpan.FromSeconds(1);
        timer.IsRepeating = true;
        timer.Tick += (_, _) => UpdateTotpCode();
        return timer;
    }

    private void UpdateTotpCode()
    {
        try
        {
            TotpCode code = _totpGenerator.GenerateCode(TotpSecret!);
            TotpDisplayCode = code.Code;
            TotpRemainingSeconds = code.RemainingSeconds;
            TotpPeriodSeconds = code.PeriodSeconds;
            TotpError = null;
        }
        catch (ArgumentException)
        {
            TotpDisplayCode = null;
            TotpError = "TOTP anahtarı geçerli bir Base32 değeri değil.";
            _totpTimer?.Stop();
        }
    }

    /// <summary>Call from the page's <c>OnDisappearing</c> so the 1-second timer doesn't keep
    /// ticking (and holding this ViewModel alive) after the user has navigated away.</summary>
    public void StopTotpTimer() => _totpTimer?.Stop();

    protected override LoginItem BuildPayload() => new(Title, Username, Password, Url, TotpSecret);

    protected override void LoadFromPayload(LoginItem payload)
    {
        Title = payload.Title;
        Username = payload.Username;
        Password = payload.Password;
        Url = payload.Url;
        TotpSecret = payload.TotpSecret;
    }
}
