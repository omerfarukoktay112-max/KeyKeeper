namespace KeyKeeper.Core.Tools;

/// <summary>
/// Copies a sensitive value (password, card number, CVV, Wi-Fi password, ...) to the clipboard
/// and automatically clears it again after a delay, so a copied secret doesn't sit on the
/// clipboard indefinitely for another app to read.
/// </summary>
public interface IClipboardClearingService
{
    /// <summary>
    /// Copies <paramref name="value"/> to the clipboard and schedules it to be cleared after
    /// <paramref name="delay"/>. If the user has copied something else in the meantime, the
    /// clipboard is left alone -- see <see cref="ClipboardClearingService"/>'s doc comment for why.
    /// Calling this again before <paramref name="delay"/> elapses replaces the pending clear with
    /// a new one for the new value.
    /// </summary>
    Task CopyAsync(string value, TimeSpan delay);
}
