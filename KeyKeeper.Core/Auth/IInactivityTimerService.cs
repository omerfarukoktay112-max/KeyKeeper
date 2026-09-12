namespace KeyKeeper.Core.Auth;

/// <summary>
/// Fires <see cref="TimedOut"/> after <see cref="Timeout"/> has elapsed with no
/// <see cref="Reset"/> call. Used by <see cref="IAuthenticationService"/> to auto-lock the
/// vault after a period of inactivity (distinct from the instant lock-on-background handled
/// separately via <c>App.OnSleep</c>).
/// </summary>
public interface IInactivityTimerService : IDisposable
{
    /// <summary>How long a period of inactivity is allowed before <see cref="TimedOut"/> fires.</summary>
    TimeSpan Timeout { get; set; }

    bool IsRunning { get; }

    event Action? TimedOut;

    /// <summary>Starts (or restarts, from zero) the countdown.</summary>
    void Start();

    /// <summary>Stops the countdown; <see cref="TimedOut"/> will not fire until <see cref="Start"/> is called again.</summary>
    void Stop();

    /// <summary>Restarts the countdown from zero, e.g. on user interaction. A no-op if not running.</summary>
    void Reset();
}
