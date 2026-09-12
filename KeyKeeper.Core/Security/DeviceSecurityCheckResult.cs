namespace KeyKeeper.Core.Security;

/// <summary>
/// The outcome of one <see cref="IDeviceSecurityChecker.CheckDeviceSecurity"/> call.
///
/// <see cref="Signals"/> is the raw list of every individual check that found evidence (e.g.
/// "su-binary-found", "build-type-not-user") -- kept around for a possible future debug/UI
/// drill-down, not surfaced to the end user today (see <c>SecurityWarningViewModel</c>, which
/// shows a plain-language message, never this list). The policy that turns evidence into a
/// warning is deliberately simple: <see cref="RequiresWarning"/> is true the moment *any* signal
/// fires, biased toward false positives over false negatives -- see
/// ARCHITECTURE.md's Phase 7 section for why that's only safe because the response is a
/// dismissable warning, never a block.
/// </summary>
public sealed record DeviceSecurityCheckResult(
    bool IsRootedOrJailbroken,
    bool IsDebuggerAttached,
    IReadOnlyList<string> Signals)
{
    public bool RequiresWarning => IsRootedOrJailbroken || IsDebuggerAttached;

    public static DeviceSecurityCheckResult Clean { get; } = new(false, false, Array.Empty<string>());
}
