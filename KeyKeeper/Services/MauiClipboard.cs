using Microsoft.Maui.ApplicationModel.DataTransfer;
using CoreIClipboard = KeyKeeper.Core.Tools.IClipboard;

namespace KeyKeeper.Services;

/// <summary><see cref="CoreIClipboard"/> backed by <see cref="Clipboard"/> -- aliased on import
/// since MAUI's own <c>DataTransfer.IClipboard</c> would otherwise collide with this app's
/// <c>KeyKeeper.Core.Tools.IClipboard</c> seam of the same name.</summary>
public sealed class MauiClipboard : CoreIClipboard
{
    public Task SetTextAsync(string? text) => Clipboard.Default.SetTextAsync(text);

    public Task<string?> GetTextAsync() => Clipboard.Default.GetTextAsync();
}
