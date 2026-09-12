namespace KeyKeeper.Core.Security;

/// <summary>
/// The testable orchestration behind the app's "device security warning" screen: runs
/// <see cref="IDeviceSecurityChecker"/> and tracks whether a warning is currently pending the
/// user's acknowledgement, without depending on any UI/MAUI type. Deliberately stateful and
/// event-free (unlike <c>IAuthenticationService.Locked</c>) -- the one caller that needs this
/// (<c>App.xaml.cs</c>'s initial-route decision, and the warning page's "continue" action) both
/// call it synchronously, so there's no case here that needs a fire-and-forget notification.
///
/// Not persisted across app launches by design: re-evaluates fresh every time
/// <see cref="EvaluateDeviceSecurity"/> is called (once, at startup), so an acknowledged warning
/// can reappear on the next cold start rather than being silently remembered forever -- see
/// ARCHITECTURE.md's Phase 7 section for why that's an acceptable, deliberately simple choice here.
/// </summary>
public interface IDeviceSecurityWarningService
{
    /// <summary>The most recent check's result, only while it still requires a warning; <c>null</c>
    /// before the first evaluation, after a clean result, or once acknowledged.</summary>
    DeviceSecurityCheckResult? PendingWarning { get; }

    bool HasPendingWarning { get; }

    /// <summary>Runs <see cref="IDeviceSecurityChecker.CheckDeviceSecurity"/> and updates
    /// <see cref="PendingWarning"/>/<see cref="HasPendingWarning"/> accordingly.</summary>
    void EvaluateDeviceSecurity();

    /// <summary>The user has seen the warning and chosen to proceed anyway. Clears
    /// <see cref="PendingWarning"/>; does not re-run the check.</summary>
    void AcknowledgeAndContinue();
}
