namespace KeyKeeper.Core.Autofill;

/// <summary>
/// Bridges the cross-platform unlock flow (<c>LockPage</c>/<c>LockViewModel</c>) to the
/// Android-only "autofill requires authentication" continuation, without either of those needing
/// an <c>#if ANDROID</c> or any reference to <c>Android.Service.Autofill.*</c> types.
///
/// The full flow (Phase 10): <c>KeyKeeperAutofillService.OnFillRequest</c> is asked to fill a
/// form field while the vault is locked. It can't hand back real credentials (the vault key isn't
/// available), so it instead builds a placeholder response whose one action, when tapped, launches
/// <c>MainActivity</c> carrying just enough state (which <c>AutofillId</c>s need filling, and the
/// matched web domain) to finish the job later — that launch is what makes
/// <see cref="HasPendingRequest"/> become <c>true</c>. The user then sees this app's own, completely
/// ordinary lock screen. On a successful unlock, <c>LockPage.OnUnlocked</c> checks
/// <see cref="HasPendingRequest"/>: if set, it calls <see cref="Complete"/> instead of navigating to
/// <c>"//vault"</c> — the Android implementation re-runs the vault lookup (now legitimately
/// possible, the vault is unlocked) and hands the real credentials back to the OS Autofill
/// framework via <c>Activity.SetResult</c>, then finishes the activity. Nothing here — or in
/// <c>LockPage</c> — ever exposes decrypted vault data to <c>KeyKeeperAutofillService</c> while the
/// vault is locked; see that service's own doc comment for the guarantee this depends on.
///
/// The iOS implementation is a permanent no-op (<see cref="HasPendingRequest"/> always
/// <c>false</c>) — iOS autofill (a Credential Provider Extension) is a different mechanism
/// entirely and out of scope for Phase 10; see ARCHITECTURE.md.
/// </summary>
public interface IAutofillCompletionHandler
{
    bool HasPendingRequest { get; }

    /// <summary>Call after a successful unlock when <see cref="HasPendingRequest"/> is true.
    /// Re-derives the real autofill response from the now-unlocked vault and delivers it to the
    /// OS, then ends the hosting activity. A no-op on iOS.</summary>
    void Complete();

    /// <summary>Call if the user backs out of the lock screen without unlocking while a request is
    /// pending, so a stale request doesn't linger into some unrelated later unlock. A no-op on
    /// iOS.</summary>
    void Cancel();
}
