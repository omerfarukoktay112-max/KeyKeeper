namespace KeyKeeper.Core.Security;

/// <summary>
/// Looks for signals that this device's OS-level security guarantees (sandboxing, Keystore/
/// Keychain-backed secure storage) might be weaker than normal -- root (Android) or jailbreak
/// (iOS), plus a debugger attached to this process.
///
/// No single check here is authoritative in either direction: a custom ROM can legitimately carry
/// a "test-keys" build tag with no root at all, and a well-hidden root can pass a naive su-binary
/// check. Implementations therefore run several independent checks and report every one that found
/// evidence via <see cref="DeviceSecurityCheckResult.Signals"/>, rather than trying to produce one
/// single "definitely rooted" verdict from one method. See the two platform implementations
/// (<c>KeyKeeper.Services.DeviceSecurityChecker</c> under <c>Platforms/Android</c> and
/// <c>Platforms/iOS</c>) and ARCHITECTURE.md's Phase 7 section for the exact signal list per
/// platform and why.
/// </summary>
public interface IDeviceSecurityChecker
{
    DeviceSecurityCheckResult CheckDeviceSecurity();
}
