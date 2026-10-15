using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeyKeeper.Core.Auth;
using KeyKeeper.Core.Crypto;
using KeyKeeper.Core.Data;
using Microsoft.Maui.Dispatching;
using KeyKeeper.Localization;

namespace KeyKeeper.ViewModels;

/// <summary>
/// Drives the lock screen: master-password unlock, optional biometric unlock, and a live
/// countdown while <see cref="LockoutPolicy"/> has the vault temporarily locked out. A freshly
/// (re)launched app won't show a countdown before the first attempt even if a lockout is already
/// in effect — <see cref="IAuthenticationService"/> deliberately doesn't expose raw lockout
/// state outside an actual unlock attempt (see its doc comment), so this only reacts to what an
/// attempt's <see cref="AuthenticationResult"/> reports.
/// </summary>
public sealed partial class LockViewModel : ObservableObject
{
    private readonly IAuthenticationService _auth;
    private readonly IBiometricAuthService _biometrics;

    private IDispatcherTimer? _lockoutTimer;
    private DateTimeOffset _lockedUntilUtc;

    public LockViewModel(IAuthenticationService auth, IBiometricAuthService biometrics)
    {
        _auth = auth;
        _biometrics = biometrics;
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UnlockCommand))]
    private string password = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? errorMessage;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UnlockCommand))]
    [NotifyCanExecuteChangedFor(nameof(UnlockWithBiometricsCommand))]
    [NotifyPropertyChangedFor(nameof(AreInputsEnabled))]
    private bool isBusy;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UnlockCommand))]
    [NotifyCanExecuteChangedFor(nameof(UnlockWithBiometricsCommand))]
    [NotifyPropertyChangedFor(nameof(AreInputsEnabled))]
    private bool isLockedOut;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UnlockWithBiometricsCommand))]
    private bool isBiometricAvailable;

    [ObservableProperty]
    private string? lockoutRemainingText;

    public bool AreInputsEnabled => !IsBusy && !IsLockedOut;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    /// <summary>Raised on a successful unlock (password or biometric) so the page can navigate
    /// away — kept out of this ViewModel to avoid giving it a Shell/navigation dependency.</summary>
    public event Action? Unlocked;

    /// <summary>Call from the page's lifecycle (e.g. <c>OnAppearing</c>) — checks biometric
    /// availability so the biometric button only shows up when it would actually work.</summary>
    public async Task InitializeAsync()
    {
        BiometricAvailability availability = await _biometrics.CheckAvailabilityAsync();
        IsBiometricAvailable = availability == BiometricAvailability.Available;
    }

    private bool CanUnlock() => AreInputsEnabled && Password.Length > 0;

    [RelayCommand(CanExecute = nameof(CanUnlock))]
    private async Task UnlockAsync()
    {
        ErrorMessage = null;
        IsBusy = true;
        char[] passwordChars = Password.ToCharArray();
        try
        {
            AuthenticationResult result = await _auth.UnlockWithMasterPasswordAsync(passwordChars);
            Apply(result);
        }
        catch (VaultDatabaseException)
        {
            ErrorMessage = S.VaultOpenFailed;
        }
        finally
        {
            SecureMemoryHelper.Clear(passwordChars);
            Password = string.Empty;
            IsBusy = false;
        }
    }

    private bool CanUseBiometrics() => AreInputsEnabled && IsBiometricAvailable;

    [RelayCommand(CanExecute = nameof(CanUseBiometrics))]
    private async Task UnlockWithBiometricsAsync()
    {
        ErrorMessage = null;
        IsBusy = true;
        try
        {
            AuthenticationResult result = await _auth.TryUnlockWithBiometricsAsync();
            Apply(result);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Apply(AuthenticationResult result)
    {
        switch (result.Kind)
        {
            case AuthenticationResultKind.Success:
                Unlocked?.Invoke();
                break;

            case AuthenticationResultKind.WrongPassword:
                ErrorMessage = S.F(S.WrongPasswordAttempt, result.FailedAttemptCount);
                if (result.RemainingLockout is { } wrongPasswordLockout)
                {
                    StartLockoutCountdown(wrongPasswordLockout);
                }

                break;

            case AuthenticationResultKind.LockedOut:
                ErrorMessage = S.TooManyAttempts;
                if (result.RemainingLockout is { } lockedOutRemaining)
                {
                    StartLockoutCountdown(lockedOutRemaining);
                }

                break;

            case AuthenticationResultKind.BiometricUnavailable:
                IsBiometricAvailable = false;
                ErrorMessage = S.BiometricUnavailable;
                break;

            case AuthenticationResultKind.BiometricFailed:
                ErrorMessage = S.BiometricFailed;
                break;
        }
    }

    private void StartLockoutCountdown(TimeSpan remaining)
    {
        _lockedUntilUtc = DateTimeOffset.UtcNow + remaining;
        IsLockedOut = true;
        UpdateLockoutRemainingText();

        _lockoutTimer ??= CreateTimer();
        _lockoutTimer.Start();
    }

    private IDispatcherTimer CreateTimer()
    {
        IDispatcherTimer timer = Application.Current!.Dispatcher.CreateTimer();
        timer.Interval = TimeSpan.FromSeconds(1);
        timer.IsRepeating = true;
        timer.Tick += (_, _) => OnLockoutTick();
        return timer;
    }

    private void OnLockoutTick()
    {
        if (DateTimeOffset.UtcNow >= _lockedUntilUtc)
        {
            _lockoutTimer?.Stop();
            IsLockedOut = false;
            LockoutRemainingText = null;
            ErrorMessage = null;
            return;
        }

        UpdateLockoutRemainingText();
    }

    private void UpdateLockoutRemainingText()
    {
        TimeSpan remaining = _lockedUntilUtc - DateTimeOffset.UtcNow;
        if (remaining < TimeSpan.Zero)
        {
            remaining = TimeSpan.Zero;
        }

        LockoutRemainingText = $"{(int)remaining.TotalMinutes:D2}:{remaining.Seconds:D2}";
    }
}
