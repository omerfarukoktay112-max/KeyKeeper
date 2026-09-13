using KeyKeeper.Core.Autofill;

namespace KeyKeeper.Services;

/// <summary>
/// iOS has no equivalent of Android's Autofill "authentication required" continuation -- iOS
/// autofill (a Credential Provider Extension, a separate app target) is a different mechanism
/// entirely and out of scope for Phase 10; see ARCHITECTURE.md. Permanent no-op: <see cref="
/// HasPendingRequest"/> is always false, so <c>LockPage.OnUnlocked</c> always falls through to its
/// ordinary <c>"//vault"</c> navigation on this platform.
/// </summary>
public sealed class AutofillCompletionHandler : IAutofillCompletionHandler
{
    public bool HasPendingRequest => false;

    public void Complete()
    {
    }

    public void Cancel()
    {
    }
}
