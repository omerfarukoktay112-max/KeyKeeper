namespace KeyKeeper.Core.Auth;

public enum AuthenticationResultKind
{
    /// <summary>The vault is now unlocked (or was just initialized and is unlocked).</summary>
    Success,

    /// <summary>Master password didn't match the stored verifier.</summary>
    WrongPassword,

    /// <summary>Too many recent wrong attempts; see <see cref="AuthenticationResult.RemainingLockout"/>.</summary>
    LockedOut,

    /// <summary>No wrapped key is stored yet, or biometric hardware/enrollment isn't available.</summary>
    BiometricUnavailable,

    /// <summary>The biometric prompt was shown but the user cancelled it or the scan failed.</summary>
    BiometricFailed,
}

/// <summary>
/// The outcome of an unlock/initialize attempt, carrying enough state (failed-attempt count,
/// remaining lockout) for the UI to show "kalan deneme hakkı / lockout süresi" without a second
/// round-trip.
/// </summary>
public sealed record AuthenticationResult(
    AuthenticationResultKind Kind,
    int FailedAttemptCount,
    TimeSpan? RemainingLockout)
{
    public static AuthenticationResult Success { get; } = new(AuthenticationResultKind.Success, 0, null);
}
