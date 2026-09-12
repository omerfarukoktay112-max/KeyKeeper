using KeyKeeper.Core.Auth;
using Microsoft.Extensions.Time.Testing;

namespace KeyKeeper.Core.Tests.Auth;

public class InactivityTimerServiceTests
{
    [Fact]
    public void Start_ThenAdvancePastTimeout_FiresTimedOut()
    {
        var time = new FakeTimeProvider();
        using var sut = new InactivityTimerService(time) { Timeout = TimeSpan.FromMinutes(2) };
        bool fired = false;
        sut.TimedOut += () => fired = true;

        sut.Start();
        time.Advance(TimeSpan.FromMinutes(2) + TimeSpan.FromSeconds(1));

        Assert.True(fired);
    }

    [Fact]
    public void Start_ThenAdvanceBeforeTimeout_DoesNotFire()
    {
        var time = new FakeTimeProvider();
        using var sut = new InactivityTimerService(time) { Timeout = TimeSpan.FromMinutes(2) };
        bool fired = false;
        sut.TimedOut += () => fired = true;

        sut.Start();
        time.Advance(TimeSpan.FromMinutes(1));

        Assert.False(fired);
    }

    [Fact]
    public void Reset_PostponesTheTimeout()
    {
        var time = new FakeTimeProvider();
        using var sut = new InactivityTimerService(time) { Timeout = TimeSpan.FromMinutes(2) };
        bool fired = false;
        sut.TimedOut += () => fired = true;

        sut.Start();
        time.Advance(TimeSpan.FromSeconds(119));
        sut.Reset();
        time.Advance(TimeSpan.FromSeconds(119));

        Assert.False(fired);

        time.Advance(TimeSpan.FromSeconds(2));

        Assert.True(fired);
    }

    [Fact]
    public void Reset_WhenNotRunning_DoesNotStartTheTimer()
    {
        var time = new FakeTimeProvider();
        using var sut = new InactivityTimerService(time) { Timeout = TimeSpan.FromMinutes(2) };
        bool fired = false;
        sut.TimedOut += () => fired = true;

        sut.Reset();
        time.Advance(TimeSpan.FromMinutes(5));

        Assert.False(fired);
        Assert.False(sut.IsRunning);
    }

    [Fact]
    public void Stop_PreventsTimedOutFromFiring()
    {
        var time = new FakeTimeProvider();
        using var sut = new InactivityTimerService(time) { Timeout = TimeSpan.FromMinutes(2) };
        bool fired = false;
        sut.TimedOut += () => fired = true;

        sut.Start();
        sut.Stop();
        time.Advance(TimeSpan.FromMinutes(5));

        Assert.False(fired);
        Assert.False(sut.IsRunning);
    }

    [Fact]
    public void IsRunning_ReflectsStartStopAndFiredState()
    {
        var time = new FakeTimeProvider();
        using var sut = new InactivityTimerService(time) { Timeout = TimeSpan.FromMinutes(2) };

        Assert.False(sut.IsRunning);

        sut.Start();
        Assert.True(sut.IsRunning);

        time.Advance(TimeSpan.FromMinutes(3));
        Assert.False(sut.IsRunning);
    }

    [Fact]
    public void Start_CalledAgainWhileRunning_RestartsTheCountdownFromZero()
    {
        var time = new FakeTimeProvider();
        using var sut = new InactivityTimerService(time) { Timeout = TimeSpan.FromMinutes(2) };
        bool fired = false;
        sut.TimedOut += () => fired = true;

        sut.Start();
        time.Advance(TimeSpan.FromSeconds(119));
        sut.Start();
        time.Advance(TimeSpan.FromSeconds(119));

        Assert.False(fired);
    }
}
