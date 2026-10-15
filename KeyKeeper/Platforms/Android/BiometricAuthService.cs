using AndroidX.Biometric;
using AndroidX.Core.Content;
using AndroidX.Fragment.App;
using Java.Lang;
using KeyKeeper.Core.Auth;
using Microsoft.Maui.ApplicationModel;
using KeyKeeper.Localization;

namespace KeyKeeper.Services;

/// <summary>
/// <see cref="IBiometricAuthService"/> backed by AndroidX's <see cref="BiometricPrompt"/> /
/// <see cref="BiometricManager"/>. Only <c>BIOMETRIC_STRONG</c> is requested (Class 3 —
/// fingerprint/face/iris backed by a Trusted Execution Environment/StrongBox), never the
/// weaker <c>BIOMETRIC_WEAK</c> class, since this gates the vault's decryption key.
/// </summary>
public sealed class BiometricAuthService : IBiometricAuthService
{
    private const int AllowedAuthenticators = (int)BiometricManager.Authenticators.BiometricStrong;

    public Task<BiometricAvailability> CheckAvailabilityAsync()
    {
        // BiometricManager.From only needs an application Context (no foreground Activity
        // required) -- unlike AuthenticateAsync below, this never shows any UI.
        BiometricManager manager = BiometricManager.From(global::Android.App.Application.Context);
        int result = manager.CanAuthenticate(AllowedAuthenticators);

        BiometricAvailability availability = result switch
        {
            BiometricManager.BiometricSuccess => BiometricAvailability.Available,
            BiometricManager.BiometricErrorNoneEnrolled => BiometricAvailability.NotEnrolled,
            BiometricManager.BiometricErrorNoHardware => BiometricAvailability.NotSupported,
            BiometricManager.BiometricErrorHwUnavailable => BiometricAvailability.NotSupported,
            BiometricManager.BiometricErrorSecurityUpdateRequired => BiometricAvailability.NotSupported,
            BiometricManager.BiometricErrorUnsupported => BiometricAvailability.NotSupported,
            _ => BiometricAvailability.Unknown, // BIOMETRIC_STATUS_UNKNOWN, or any future status code
        };

        return Task.FromResult(availability);
    }

    public Task<BiometricAuthOutcome> AuthenticateAsync(string reason)
    {
        // BiometricPrompt needs a live FragmentActivity to host its UI (it's backed by a
        // DialogFragment attached to the activity's fragment manager). Platform.CurrentActivity
        // can be null (no foreground activity right now) or -- in principle, if this app's
        // activity hierarchy ever changed -- not a FragmentActivity; MainActivity itself is a
        // MauiAppCompatActivity, which is one, but this cast stays defensive rather than assumed.
        if (Platform.CurrentActivity is not FragmentActivity activity)
        {
            return Task.FromResult(BiometricAuthOutcome.NotAvailable);
        }

        var tcs = new TaskCompletionSource<BiometricAuthOutcome>();

        // BiometricPrompt's construction and Authenticate() call both internally drive the
        // FragmentManager (it's backed by a DialogFragment) and throw IllegalStateException
        // ("Must be called from main thread of fragment host") off the UI thread. Callers of
        // this method may not still be on it: AuthenticationService.TryUnlockWithBiometricsAsync
        // awaits IWrappedKeyStorage.RetrieveAsync() first, and that await's continuation isn't
        // guaranteed to land back on the UI thread depending on how the secure-storage call is
        // implemented -- confirmed by hitting this exact crash on-device before adding this
        // dispatch (see ARCHITECTURE.md's Phase 3 manual-verification notes).
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                var callback = new AuthenticationCallback(tcs);
                Java.Util.Concurrent.IExecutor executor = ContextCompat.GetMainExecutor(activity)
                    ?? throw new InvalidOperationException("ContextCompat.GetMainExecutor unexpectedly returned null.");
                var prompt = new BiometricPrompt(activity, executor, callback);

                BiometricPrompt.PromptInfo promptInfo = new BiometricPrompt.PromptInfo.Builder()
                    .SetTitle(S.BiometricPromptTitle)
                    .SetSubtitle(reason)
                    .SetNegativeButtonText(S.Cancel)
                    .SetAllowedAuthenticators(AllowedAuthenticators)
                    .Build();

                prompt.Authenticate(promptInfo);
            }
            catch (System.Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });

        return tcs.Task;
    }

    /// <summary>
    /// Maps AndroidX's <c>BiometricPrompt.AuthenticationCallback</c> (which only speaks in
    /// terms of Android's own error codes) onto the shared <see cref="BiometricAuthOutcome"/>
    /// that <see cref="Core.Auth.AuthenticationService"/> actually understands, so both this
    /// class and the iOS <c>BiometricAuthService</c> speak the same contract to it.
    /// </summary>
    private sealed class AuthenticationCallback(TaskCompletionSource<BiometricAuthOutcome> tcs)
        : BiometricPrompt.AuthenticationCallback
    {
        public override void OnAuthenticationSucceeded(BiometricPrompt.AuthenticationResult result) =>
            tcs.TrySetResult(BiometricAuthOutcome.Success);

        public override void OnAuthenticationError(int errorCode, ICharSequence? errString)
        {
            BiometricAuthOutcome outcome = errorCode switch
            {
                BiometricPrompt.ErrorUserCanceled => BiometricAuthOutcome.Cancelled,
                BiometricPrompt.ErrorNegativeButton => BiometricAuthOutcome.Cancelled,
                BiometricPrompt.ErrorCanceled => BiometricAuthOutcome.Cancelled,

                BiometricPrompt.ErrorNoBiometrics => BiometricAuthOutcome.NotAvailable,
                BiometricPrompt.ErrorHwNotPresent => BiometricAuthOutcome.NotAvailable,
                BiometricPrompt.ErrorHwUnavailable => BiometricAuthOutcome.NotAvailable,
                BiometricPrompt.ErrorSecurityUpdateRequired => BiometricAuthOutcome.NotAvailable,
                BiometricPrompt.ErrorNoDeviceCredential => BiometricAuthOutcome.NotAvailable,

                // ErrorLockout (temporary, 30s) / ErrorLockoutPermanent (needs device-credential
                // unlock to reset) both mean "too many wrong biometric attempts" -- the hardware
                // itself is fine, this specific attempt failed, so Failed (not NotAvailable) is
                // the honest mapping. Per IAuthenticationService's contract, a failed/cancelled
                // biometric attempt never touches the master-password lockout counter (see
                // AuthenticationServiceTests) -- that guarantee holds regardless of this mapping,
                // since AuthenticationService.TryUnlockWithBiometricsAsync never writes lockout
                // state at all.
                _ => BiometricAuthOutcome.Failed,
            };

            tcs.TrySetResult(outcome);
        }

        public override void OnAuthenticationFailed()
        {
            // A single scan didn't match (e.g. wrong finger) -- BiometricPrompt keeps the sheet
            // open for the user to retry. This is not a terminal outcome: don't complete the
            // task. The prompt eventually calls OnAuthenticationError (e.g. ErrorLockout) if
            // retries are exhausted, or OnAuthenticationSucceeded if a later attempt matches.
        }
    }
}
