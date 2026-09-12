namespace KeyKeeper.Core.Tools;

/// <summary>
/// <see cref="IClipboardClearingService"/> built on <see cref="TimeProvider"/> (same pattern as
/// <see cref="Auth.InactivityTimerService"/>, testable with
/// <c>Microsoft.Extensions.Time.Testing.FakeTimeProvider</c>) and an injectable
/// <see cref="IClipboard"/> (testable with a fake clipboard).
///
/// Before clearing, re-reads the clipboard and only clears it if it still holds exactly the value
/// this call copied there. Without that check, clearing on a bare timeout would blow away
/// something the user copied from elsewhere in the meantime -- e.g. copy a password, immediately
/// copy an unrelated URL to paste somewhere else, and have this timer wipe *that* a few seconds
/// later even though it never touched the clipboard itself.
/// </summary>
public sealed class ClipboardClearingService : IClipboardClearingService, IDisposable
{
    private readonly IClipboard _clipboard;
    private readonly TimeProvider _timeProvider;
    private readonly object _gate = new();
    private ITimer? _timer;
    private string? _pendingValue;

    public ClipboardClearingService(IClipboard clipboard, TimeProvider? timeProvider = null)
    {
        _clipboard = clipboard;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task CopyAsync(string value, TimeSpan delay)
    {
        await _clipboard.SetTextAsync(value).ConfigureAwait(false);

        lock (_gate)
        {
            _timer?.Dispose();
            _pendingValue = value;
            _timer = _timeProvider.CreateTimer(OnElapsed, null, delay, System.Threading.Timeout.InfiniteTimeSpan);
        }
    }

    private void OnElapsed(object? state)
    {
        string? expected;
        lock (_gate)
        {
            expected = _pendingValue;
            _timer?.Dispose();
            _timer = null;
            _pendingValue = null;
        }

        if (expected is null)
        {
            return;
        }

        _ = ClearIfUnchangedAsync(expected);
    }

    private async Task ClearIfUnchangedAsync(string expectedValue)
    {
        string? current = await _clipboard.GetTextAsync().ConfigureAwait(false);
        if (string.Equals(current, expectedValue, StringComparison.Ordinal))
        {
            await _clipboard.SetTextAsync(string.Empty).ConfigureAwait(false);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _timer?.Dispose();
            _timer = null;
        }
    }
}
