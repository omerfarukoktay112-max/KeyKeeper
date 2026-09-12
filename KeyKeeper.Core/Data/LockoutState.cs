namespace KeyKeeper.Core.Data;

/// <summary>
/// Persisted failed-master-password-attempt counter and lockout expiry. Must survive app
/// restart (hence stored, not kept in memory) so a locked-out attacker can't bypass the
/// lockout by force-closing and relaunching the app.
/// </summary>
/// <param name="FailedAttemptCount">Consecutive wrong-password attempts since the last success.</param>
/// <param name="LockedUntilUtc">
/// If set and in the future, further attempts are rejected until this time without even
/// running the (deliberately slow) KDF — see <c>KeyKeeper.Core.Auth.LockoutPolicy</c>.
/// </param>
public sealed record LockoutState(int FailedAttemptCount, DateTimeOffset? LockedUntilUtc)
{
    public static LockoutState None { get; } = new(0, null);
}
