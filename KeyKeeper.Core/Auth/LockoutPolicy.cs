namespace KeyKeeper.Core.Auth;

/// <summary>One escalation step: at or above <see cref="FailureThreshold"/> consecutive wrong-password attempts, lock out for <see cref="LockoutDuration"/>.</summary>
public sealed record LockoutTier(int FailureThreshold, TimeSpan LockoutDuration);

/// <summary>
/// Progressive lockout schedule for wrong master-password attempts. A documented, tunable
/// placeholder (matches the spirit of <c>KeyKeeper.Core.Crypto.Argon2Constants</c>) — recalibrate
/// once there's real-world data on how this affects legitimate users vs. attackers.
/// </summary>
public static class LockoutPolicy
{
    public static readonly IReadOnlyList<LockoutTier> Tiers =
    [
        new(FailureThreshold: 3, LockoutDuration: TimeSpan.FromSeconds(5)),
        new(FailureThreshold: 5, LockoutDuration: TimeSpan.FromSeconds(30)),
        new(FailureThreshold: 8, LockoutDuration: TimeSpan.FromMinutes(2)),
        new(FailureThreshold: 12, LockoutDuration: TimeSpan.FromMinutes(15)),
    ];

    /// <summary>
    /// The lockout duration that applies once <paramref name="failedAttemptCount"/> consecutive
    /// failures have occurred, or null if that count hasn't reached the first tier yet.
    /// </summary>
    public static TimeSpan? GetLockoutDuration(int failedAttemptCount)
    {
        TimeSpan? duration = null;
        foreach (LockoutTier tier in Tiers)
        {
            if (failedAttemptCount >= tier.FailureThreshold)
            {
                duration = tier.LockoutDuration;
            }
        }

        return duration;
    }
}
