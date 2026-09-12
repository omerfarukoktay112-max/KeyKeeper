using KeyKeeper.Core.Auth;

namespace KeyKeeper.Core.Tests.Auth;

public class LockoutPolicyTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void GetLockoutDuration_BelowFirstTier_ReturnsNull(int failedAttemptCount)
    {
        Assert.Null(LockoutPolicy.GetLockoutDuration(failedAttemptCount));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public void GetLockoutDuration_AtFirstTier_ReturnsFiveSeconds(int failedAttemptCount)
    {
        Assert.Equal(TimeSpan.FromSeconds(5), LockoutPolicy.GetLockoutDuration(failedAttemptCount));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(7)]
    public void GetLockoutDuration_AtSecondTier_ReturnsThirtySeconds(int failedAttemptCount)
    {
        Assert.Equal(TimeSpan.FromSeconds(30), LockoutPolicy.GetLockoutDuration(failedAttemptCount));
    }

    [Theory]
    [InlineData(8)]
    [InlineData(11)]
    public void GetLockoutDuration_AtThirdTier_ReturnsTwoMinutes(int failedAttemptCount)
    {
        Assert.Equal(TimeSpan.FromMinutes(2), LockoutPolicy.GetLockoutDuration(failedAttemptCount));
    }

    [Theory]
    [InlineData(12)]
    [InlineData(100)]
    public void GetLockoutDuration_AtOrBeyondFinalTier_ReturnsFifteenMinutes(int failedAttemptCount)
    {
        Assert.Equal(TimeSpan.FromMinutes(15), LockoutPolicy.GetLockoutDuration(failedAttemptCount));
    }
}
