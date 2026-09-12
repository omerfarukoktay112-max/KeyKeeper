using KeyKeeper.Core.Tools;
using Microsoft.Extensions.Time.Testing;

namespace KeyKeeper.Core.Tests.Tools;

/// <summary>In-memory <see cref="IClipboard"/> double -- lets tests both drive and observe the
/// clipboard without any platform dependency.</summary>
internal sealed class FakeClipboard : IClipboard
{
    public string? Text { get; private set; }

    public Task SetTextAsync(string? text)
    {
        Text = text;
        return Task.CompletedTask;
    }

    public Task<string?> GetTextAsync() => Task.FromResult(Text);
}

public class ClipboardClearingServiceTests
{
    [Fact]
    public async Task CopyAsync_SetsClipboardImmediately()
    {
        var clipboard = new FakeClipboard();
        var sut = new ClipboardClearingService(clipboard, new FakeTimeProvider());

        await sut.CopyAsync("s3cret", TimeSpan.FromSeconds(30));

        Assert.Equal("s3cret", clipboard.Text);
    }

    [Fact]
    public async Task CopyAsync_AfterDelayElapses_ClearsClipboard()
    {
        var clipboard = new FakeClipboard();
        var time = new FakeTimeProvider();
        var sut = new ClipboardClearingService(clipboard, time);

        await sut.CopyAsync("s3cret", TimeSpan.FromSeconds(30));
        time.Advance(TimeSpan.FromSeconds(31));

        Assert.Equal(string.Empty, clipboard.Text);
    }

    [Fact]
    public async Task CopyAsync_BeforeDelayElapses_DoesNotClearClipboard()
    {
        var clipboard = new FakeClipboard();
        var time = new FakeTimeProvider();
        var sut = new ClipboardClearingService(clipboard, time);

        await sut.CopyAsync("s3cret", TimeSpan.FromSeconds(30));
        time.Advance(TimeSpan.FromSeconds(29));

        Assert.Equal("s3cret", clipboard.Text);
    }

    [Fact]
    public async Task CopyAsync_UserChangesClipboardBeforeDelayElapses_DoesNotOverwriteTheirValue()
    {
        var clipboard = new FakeClipboard();
        var time = new FakeTimeProvider();
        var sut = new ClipboardClearingService(clipboard, time);

        await sut.CopyAsync("s3cret", TimeSpan.FromSeconds(30));
        await clipboard.SetTextAsync("something the user copied themselves");
        time.Advance(TimeSpan.FromSeconds(31));

        Assert.Equal("something the user copied themselves", clipboard.Text);
    }

    [Fact]
    public async Task CopyAsync_CalledAgainBeforeFirstDelayElapses_OnlyClearsAfterTheSecondDelay()
    {
        var clipboard = new FakeClipboard();
        var time = new FakeTimeProvider();
        var sut = new ClipboardClearingService(clipboard, time);

        await sut.CopyAsync("first", TimeSpan.FromSeconds(30));
        time.Advance(TimeSpan.FromSeconds(20));
        await sut.CopyAsync("second", TimeSpan.FromSeconds(30));

        // The first timer's original 30s mark (10s from here) should no longer clear anything.
        time.Advance(TimeSpan.FromSeconds(11));
        Assert.Equal("second", clipboard.Text);

        time.Advance(TimeSpan.FromSeconds(20));
        Assert.Equal(string.Empty, clipboard.Text);
    }
}
