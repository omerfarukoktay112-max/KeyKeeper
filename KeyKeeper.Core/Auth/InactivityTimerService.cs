namespace KeyKeeper.Core.Auth;

/// <summary>
/// <see cref="IInactivityTimerService"/> built on <see cref="TimeProvider"/> (not
/// <see cref="System.Threading.Timer"/> directly), so tests can advance a fake clock instead of
/// waiting on wall-clock time — see <c>Microsoft.Extensions.Time.Testing.FakeTimeProvider</c> in
/// <c>KeyKeeper.Core.Tests</c>.
/// </summary>
public sealed class InactivityTimerService : IInactivityTimerService
{
    private readonly TimeProvider _timeProvider;
    private readonly object _gate = new();
    private ITimer? _timer;

    public InactivityTimerService(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        Timeout = TimeSpan.FromMinutes(2);
    }

    public TimeSpan Timeout { get; set; }

    public bool IsRunning { get; private set; }

    public event Action? TimedOut;

    public void Start()
    {
        lock (_gate)
        {
            _timer?.Dispose();
            _timer = _timeProvider.CreateTimer(OnElapsed, null, Timeout, System.Threading.Timeout.InfiniteTimeSpan);
            IsRunning = true;
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            if (!IsRunning)
            {
                return;
            }

            _timer?.Change(Timeout, System.Threading.Timeout.InfiniteTimeSpan);
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            _timer?.Dispose();
            _timer = null;
            IsRunning = false;
        }
    }

    private void OnElapsed(object? state)
    {
        lock (_gate)
        {
            // The timer already fired (one-shot); drop our reference so Stop()/Dispose() don't
            // double-dispose it, but don't touch IsRunning under the lock beyond this -- the
            // TimedOut handler (typically AuthenticationService.Lock) may itself call Stop().
            _timer = null;
            IsRunning = false;
        }

        TimedOut?.Invoke();
    }

    public void Dispose() => Stop();
}
