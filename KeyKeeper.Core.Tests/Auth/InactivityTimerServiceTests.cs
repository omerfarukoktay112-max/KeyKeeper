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

    /// <summary>
    /// Models real, sustained UI use (see <c>KeyKeeper.Behaviors.PageActivityBehavior</c>): a
    /// stream of small periodic <see cref="InactivityTimerService.Reset"/> calls, each well under
    /// <see cref="InactivityTimerService.Timeout"/> apart, spanning a total elapsed time several
    /// times longer than <see cref="InactivityTimerService.Timeout"/> itself. Before the app-wide
    /// activity wiring existed, nothing called <c>Reset</c> at all, so this exact scenario would
    /// have fired at the first <c>Timeout</c> regardless of the interaction in between -- this is
    /// the regression test for that gap.
    /// </summary>
    [Fact]
    public void Reset_CalledPeriodicallyAcrossSeveralTimeoutWindows_NeverFires()
    {
        var time = new FakeTimeProvider();
        using var sut = new InactivityTimerService(time) { Timeout = TimeSpan.FromMinutes(2) };
        bool fired = false;
        sut.TimedOut += () => fired = true;

        sut.Start();
        for (int i = 0; i < 10; i++)
        {
            time.Advance(TimeSpan.FromSeconds(30));
            sut.Reset();
        }

        Assert.False(fired);
        Assert.True(sut.IsRunning);
    }

    /// <summary>
    /// The other half of the same scenario: sustained activity postpones the lock only while it
    /// keeps happening -- once it genuinely stops, <see cref="InactivityTimerService.Timeout"/> still elapses and
    /// <see cref="InactivityTimerService.TimedOut"/> still fires, measured from the *last*
    /// <see cref="InactivityTimerService.Reset"/>, not from the original <see cref="InactivityTimerService.Start"/>.
    /// </summary>
    [Fact]
    public void Reset_CalledPeriodicallyThenStops_StillFiresTimeoutAfterLastReset()
    {
        var time = new FakeTimeProvider();
        using var sut = new InactivityTimerService(time) { Timeout = TimeSpan.FromMinutes(2) };
        bool fired = false;
        sut.TimedOut += () => fired = true;

        sut.Start();
        for (int i = 0; i < 5; i++)
        {
            time.Advance(TimeSpan.FromSeconds(30));
            sut.Reset();
        }

        Assert.False(fired);

        time.Advance(TimeSpan.FromMinutes(2) - TimeSpan.FromSeconds(1));
        Assert.False(fired);

        time.Advance(TimeSpan.FromSeconds(2));
        Assert.True(fired);
    }
}
