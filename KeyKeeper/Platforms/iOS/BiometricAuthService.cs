using Foundation;
using KeyKeeper.Core.Auth;
using LocalAuthentication;

namespace KeyKeeper.Services;

/// <summary>
/// <see cref="IBiometricAuthService"/> backed by <c>LocalAuthentication</c>'s
/// <see cref="LAContext"/>, using <see cref="LAPolicy.DeviceOwnerAuthenticationWithBiometrics"/>
/// (Face ID / Touch ID only — never falls back to the device passcode, unlike
/// <c>DeviceOwnerAuthentication</c>) so this stays the biometric-specific counterpart to Android's
/// <c>BiometricAuthService</c> (BIOMETRIC_STRONG only, no device-credential fallback there
/// either).
///
/// Requires <c>NSFaceIDUsageDescription</c> in Info.plist — without it, iOS silently fails any
/// Face ID request rather than prompting.
/// </summary>
public sealed class BiometricAuthService : IBiometricAuthService
{
    private const LAPolicy Policy = LAPolicy.DeviceOwnerAuthenticationWithBiometrics;

    public Task<BiometricAvailability> CheckAvailabilityAsync()
    {
        using var context = new LAContext();
        bool canEvaluate = context.CanEvaluatePolicy(Policy, out NSError? error);

        BiometricAvailability availability = canEvaluate
            ? BiometricAvailability.Available
            : MapUnavailability(error);

        return Task.FromResult(availability);
    }

    public async Task<BiometricAuthOutcome> AuthenticateAsync(string reason)
    {
        using var context = new LAContext();

        // Mirrors the Android implementation's pre-check: don't even attempt to show a prompt
        // (EvaluatePolicyAsync would just fail immediately anyway) if biometrics aren't usable.
        if (!context.CanEvaluatePolicy(Policy, out _))
        {
            return BiometricAuthOutcome.NotAvailable;
        }

        try
        {
            (bool success, NSError? error) = await context.EvaluatePolicyAsync(Policy, reason).ConfigureAwait(false);
            return success ? BiometricAuthOutcome.Success : MapEvaluationFailure(error);
        }
        catch (Exception)
        {
            // EvaluatePolicyAsync throwing at all is not expected in normal operation (errors are
            // meant to come back via the NSError out value above) -- treat it the same as an
            // unrecognized LAStatus rather than letting it escape as an unhandled exception.
            return BiometricAuthOutcome.Failed;
        }
    }

    /// <summary>
    /// Maps a <see cref="LAContext.CanEvaluatePolicy"/> failure -- i.e. biometrics aren't
    /// currently usable at all, before any prompt is shown -- onto <see cref="BiometricAvailability"/>.
    /// </summary>
    private static BiometricAvailability MapUnavailability(NSError? error)
    {
        if (error is null)
        {
            return BiometricAvailability.Unknown;
        }

        var status = (LAStatus)(long)error.Code;
        return status switch
        {
            LAStatus.BiometryNotEnrolled => BiometricAvailability.NotEnrolled,
            LAStatus.BiometryNotAvailable => BiometricAvailability.NotSupported,
            LAStatus.PasscodeNotSet => BiometricAvailability.NotSupported,

            // Already locked out from too many prior failed attempts. There's no dedicated
            // BiometricAvailability case for "temporarily locked" (Android's BiometricManager
            // .CanAuthenticate doesn't expose lockout state at the pre-check stage either -- it
            // only surfaces via BiometricPrompt.ERROR_LOCKOUT once you actually try), so both
            // platforms treat a pre-check failure here the same way: not currently usable.
            LAStatus.BiometryLockout => BiometricAvailability.NotSupported,

            _ => BiometricAvailability.Unknown,
        };
    }

    /// <summary>
    /// Maps an <see cref="LAContext.EvaluatePolicyAsync"/> failure -- the prompt was shown but
    /// didn't succeed -- onto <see cref="BiometricAuthOutcome"/>, symmetric with the Android
    /// implementation's <c>BiometricPrompt.AuthenticationCallback.OnAuthenticationError</c> map.
    /// </summary>
    private static BiometricAuthOutcome MapEvaluationFailure(NSError? error)
    {
        if (error is null)
        {
            return BiometricAuthOutcome.Failed;
        }

        var status = (LAStatus)(long)error.Code;
        return status switch
        {
            LAStatus.UserCancel => BiometricAuthOutcome.Cancelled,
            LAStatus.AppCancel => BiometricAuthOutcome.Cancelled,
            LAStatus.SystemCancel => BiometricAuthOutcome.Cancelled,
            LAStatus.UserFallback => BiometricAuthOutcome.Cancelled,

            LAStatus.BiometryNotAvailable => BiometricAuthOutcome.NotAvailable,
            LAStatus.BiometryNotEnrolled => BiometricAuthOutcome.NotAvailable,
            LAStatus.PasscodeNotSet => BiometricAuthOutcome.NotAvailable,

            // BiometryLockout (too many wrong attempts) and everything else (AuthenticationFailed,
            // InvalidContext, NotInteractive, ...) are attempt failures, not unavailability --
            // symmetric with Android's ErrorLockout/ErrorLockoutPermanent mapping to Failed. As on
            // Android, this never touches the master-password lockout counter either way, since
            // AuthenticationService.TryUnlockWithBiometricsAsync never writes lockout state.
            _ => BiometricAuthOutcome.Failed,
        };
    }
}
