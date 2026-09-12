using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeyKeeper.Core.Tools;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Graphics;

namespace KeyKeeper.ViewModels;

/// <summary>Drives the standalone "Şifre Üretici" tool page. <see cref="LoginFormViewModel"/> has
/// its own, simpler <c>GeneratePasswordCommand</c> for the in-form "Üret" button (fixed 16-char,
/// every category) — this page is for generating a password to use somewhere outside the vault,
/// so it exposes every <see cref="PasswordGeneratorOptions"/> knob and a "Kopyala" button.</summary>
public sealed partial class PasswordGeneratorViewModel : ObservableObject
{
    private readonly IPasswordGenerator _generator;
    private readonly IPasswordStrengthAnalyzer _strengthAnalyzer;
    private readonly IClipboardClearingService _clipboardClearingService;

    private static readonly TimeSpan ClipboardClearDelay = TimeSpan.FromSeconds(30);

    private IDispatcherTimer? _clipboardCountdownTimer;
    private DateTimeOffset _clipboardClearAtUtc;

    public PasswordGeneratorViewModel(IPasswordGenerator generator, IPasswordStrengthAnalyzer strengthAnalyzer, IClipboardClearingService clipboardClearingService)
    {
        _generator = generator;
        _strengthAnalyzer = strengthAnalyzer;
        _clipboardClearingService = clipboardClearingService;
    }

    [ObservableProperty]
    private int length = 16;

    [ObservableProperty]
    private bool includeUppercase = true;

    [ObservableProperty]
    private bool includeLowercase = true;

    [ObservableProperty]
    private bool includeDigits = true;

    [ObservableProperty]
    private bool includeSpecialCharacters = true;

    [ObservableProperty]
    private bool excludeAmbiguousCharacters;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPassword))]
    private string? generatedPassword;

    public bool HasPassword => !string.IsNullOrEmpty(GeneratedPassword);

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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? errorMessage;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasClipboardStatus))]
    private string? clipboardStatus;

    public bool HasClipboardStatus => !string.IsNullOrEmpty(ClipboardStatus);

    [RelayCommand]
    private void Generate()
    {
        ErrorMessage = null;
        ClipboardStatus = null;
        try
        {
            GeneratedPassword = _generator.Generate(new PasswordGeneratorOptions
            {
                Length = Length,
                IncludeUppercase = IncludeUppercase,
                IncludeLowercase = IncludeLowercase,
                IncludeDigits = IncludeDigits,
                IncludeSpecialCharacters = IncludeSpecialCharacters,
                ExcludeAmbiguousCharacters = ExcludeAmbiguousCharacters,
            });
            PasswordStrength = _strengthAnalyzer.Analyze(GeneratedPassword);
        }
        catch (ArgumentException ex)
        {
            GeneratedPassword = null;
            PasswordStrength = null;
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task CopyAsync()
    {
        if (string.IsNullOrEmpty(GeneratedPassword))
        {
            return;
        }

        await _clipboardClearingService.CopyAsync(GeneratedPassword, ClipboardClearDelay);
        StartClipboardCountdown();
    }

    private void StartClipboardCountdown()
    {
        _clipboardClearAtUtc = DateTimeOffset.UtcNow + ClipboardClearDelay;
        UpdateClipboardStatus();

        _clipboardCountdownTimer ??= CreateClipboardCountdownTimer();
        _clipboardCountdownTimer.Start();
    }

    private IDispatcherTimer CreateClipboardCountdownTimer()
    {
        IDispatcherTimer timer = Application.Current!.Dispatcher.CreateTimer();
        timer.Interval = TimeSpan.FromSeconds(1);
        timer.IsRepeating = true;
        timer.Tick += (_, _) => OnClipboardCountdownTick();
        return timer;
    }

    private void OnClipboardCountdownTick()
    {
        if (DateTimeOffset.UtcNow >= _clipboardClearAtUtc)
        {
            _clipboardCountdownTimer?.Stop();
            ClipboardStatus = null;
            return;
        }

        UpdateClipboardStatus();
    }

    private void UpdateClipboardStatus()
    {
        TimeSpan remaining = _clipboardClearAtUtc - DateTimeOffset.UtcNow;
        if (remaining < TimeSpan.Zero)
        {
            remaining = TimeSpan.Zero;
        }

        ClipboardStatus = $"Panoya kopyalandı, {(int)Math.Ceiling(remaining.TotalSeconds)} saniye sonra temizlenecek.";
    }

    /// <summary>Call from the page's <c>OnDisappearing</c> so the 1-second countdown timer
    /// doesn't keep ticking after the user has navigated away.</summary>
    public void StopClipboardCountdown() => _clipboardCountdownTimer?.Stop();
}
