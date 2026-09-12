namespace KeyKeeper.Core.Auth;

public enum BiometricAvailability
{
    Available,
    NotEnrolled,
    NotSupported,
    Unknown,
}

public enum BiometricAuthOutcome
{
    Success,
    Cancelled,
    Failed,
    NotAvailable,
}

/// <summary>
/// Thin wrapper over the platform's biometric prompt (Android BiometricPrompt / iOS
/// LocalAuthentication). Implemented per-platform in the MAUI head project — see
/// <c>KeyKeeper/Platforms/Android/BiometricAuthService.cs</c> and
/// <c>KeyKeeper/Platforms/iOS/BiometricAuthService.cs</c> — kept intentionally free of any
/// vault/key logic: it only answers "is biometric auth available" and "did the user just
/// authenticate". <see cref="IAuthenticationService"/> is what turns a
/// <see cref="BiometricAuthOutcome.Success"/> into actually unlocking the vault.
/// </summary>
public interface IBiometricAuthService
{
    Task<BiometricAvailability> CheckAvailabilityAsync();

    /// <param name="reason">Shown to the user as the prompt's subtitle/reason for the request.</param>
    Task<BiometricAuthOutcome> AuthenticateAsync(string reason);
}
