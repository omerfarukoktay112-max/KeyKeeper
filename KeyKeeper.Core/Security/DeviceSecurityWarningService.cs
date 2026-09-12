namespace KeyKeeper.Core.Security;

public sealed class DeviceSecurityWarningService : IDeviceSecurityWarningService
{
    private readonly IDeviceSecurityChecker _checker;

    public DeviceSecurityWarningService(IDeviceSecurityChecker checker)
    {
        _checker = checker;
    }

    public DeviceSecurityCheckResult? PendingWarning { get; private set; }

    public bool HasPendingWarning => PendingWarning is not null;

    public void EvaluateDeviceSecurity()
    {
        DeviceSecurityCheckResult result = _checker.CheckDeviceSecurity();
        PendingWarning = result.RequiresWarning ? result : null;
    }

    public void AcknowledgeAndContinue() => PendingWarning = null;
}
