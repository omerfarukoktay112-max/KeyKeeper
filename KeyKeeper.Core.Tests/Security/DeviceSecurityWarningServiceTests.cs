using KeyKeeper.Core.Security;

namespace KeyKeeper.Core.Tests.Security;

public class DeviceSecurityWarningServiceTests
{
    /// <summary>Stands in for a real platform checker -- same "fake the OS-dependent seam" style
    /// as <c>AuthenticationServiceTests.FakeBiometricAuthService</c>.</summary>
    private sealed class FakeDeviceSecurityChecker : IDeviceSecurityChecker
    {
        public DeviceSecurityCheckResult Result { get; set; } = DeviceSecurityCheckResult.Clean;

        public DeviceSecurityCheckResult CheckDeviceSecurity() => Result;
    }

    [Fact]
    public void EvaluateDeviceSecurity_CleanResult_NoPendingWarning()
    {
        var checker = new FakeDeviceSecurityChecker { Result = DeviceSecurityCheckResult.Clean };
        var service = new DeviceSecurityWarningService(checker);

        service.EvaluateDeviceSecurity();

        Assert.False(service.HasPendingWarning);
        Assert.Null(service.PendingWarning);
    }

    [Fact]
    public void EvaluateDeviceSecurity_Rooted_SetsPendingWarning()
    {
        var result = new DeviceSecurityCheckResult(true, false, new[] { "su-binary-found" });
        var checker = new FakeDeviceSecurityChecker { Result = result };
        var service = new DeviceSecurityWarningService(checker);

        service.EvaluateDeviceSecurity();

        Assert.True(service.HasPendingWarning);
        Assert.Same(result, service.PendingWarning);
    }

    [Fact]
    public void EvaluateDeviceSecurity_DebuggerAttached_SetsPendingWarning()
    {
        var result = new DeviceSecurityCheckResult(false, true, new[] { "debugger-attached" });
        var checker = new FakeDeviceSecurityChecker { Result = result };
        var service = new DeviceSecurityWarningService(checker);

        service.EvaluateDeviceSecurity();

        Assert.True(service.HasPendingWarning);
    }

    [Fact]
    public void EvaluateDeviceSecurity_BothRootedAndDebugged_SetsPendingWarning()
    {
        var result = new DeviceSecurityCheckResult(true, true, new[] { "su-binary-found", "debugger-attached" });
        var checker = new FakeDeviceSecurityChecker { Result = result };
        var service = new DeviceSecurityWarningService(checker);

        service.EvaluateDeviceSecurity();

        Assert.True(service.HasPendingWarning);
        Assert.Equal(2, service.PendingWarning!.Signals.Count);
    }

    [Fact]
    public void AcknowledgeAndContinue_ClearsPendingWarning()
    {
        var checker = new FakeDeviceSecurityChecker
        {
            Result = new DeviceSecurityCheckResult(true, false, new[] { "su-binary-found" }),
        };
        var service = new DeviceSecurityWarningService(checker);
        service.EvaluateDeviceSecurity();

        service.AcknowledgeAndContinue();

        Assert.False(service.HasPendingWarning);
        Assert.Null(service.PendingWarning);
    }

    [Fact]
    public void AcknowledgeAndContinue_DoesNotReRunCheck()
    {
        var checker = new FakeDeviceSecurityChecker
        {
            Result = new DeviceSecurityCheckResult(true, false, new[] { "su-binary-found" }),
        };
        var service = new DeviceSecurityWarningService(checker);
        service.EvaluateDeviceSecurity();

        // Flip the fake to "clean" before acknowledging -- AcknowledgeAndContinue must not
        // re-check, only clear the already-pending warning.
        checker.Result = DeviceSecurityCheckResult.Clean;
        service.AcknowledgeAndContinue();

        Assert.False(service.HasPendingWarning);
    }

    [Fact]
    public void EvaluateDeviceSecurity_CalledAgainAfterAcknowledge_CanResurfaceWarning()
    {
        var result = new DeviceSecurityCheckResult(true, false, new[] { "su-binary-found" });
        var checker = new FakeDeviceSecurityChecker { Result = result };
        var service = new DeviceSecurityWarningService(checker);
        service.EvaluateDeviceSecurity();
        service.AcknowledgeAndContinue();

        // Simulates a fresh cold start re-evaluating from scratch -- no persisted acknowledgement.
        service.EvaluateDeviceSecurity();

        Assert.True(service.HasPendingWarning);
    }
}
