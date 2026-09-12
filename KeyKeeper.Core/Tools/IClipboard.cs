namespace KeyKeeper.Core.Tools;

/// <summary>
/// A minimal abstraction over the platform clipboard (<c>Microsoft.Maui.ApplicationModel.DataTransfer.Clipboard</c>
/// in the MAUI head project) -- exists purely as a seam so <see cref="ClipboardClearingService"/>
/// stays unit-testable with a fake clipboard, same reasoning as
/// <see cref="Storage.ISecureKeyValueStore"/>. See <c>KeyKeeper.Services.MauiClipboard</c> for the
/// real implementation.
/// </summary>
public interface IClipboard
{
    Task SetTextAsync(string? text);

    /// <summary>Returns null if the clipboard is empty or holds non-text content.</summary>
    Task<string?> GetTextAsync();
}
