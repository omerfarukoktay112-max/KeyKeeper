using System.Runtime.Versioning;
using Android.App;
using Android.Content;
using Android.Service.Autofill;
using Android.Views.Autofill;
using KeyKeeper.Core.Auth;
using KeyKeeper.Core.Autofill;
using KeyKeeper.Core.Data;
using Microsoft.Maui.ApplicationModel;

namespace KeyKeeper.Services;

/// <summary>
/// Android <see cref="IAutofillCompletionHandler"/> -- see that interface's doc comment for the
/// full "auth-required autofill" flow. Registered as a singleton (Phase 10, <c>MauiProgram.cs</c>)
/// so <see cref="MainActivity"/> (setting the pending request when launched via
/// <see cref="KeyKeeperAutofillService"/>'s authentication <c>PendingIntent</c>) and
/// <c>LockPage</c> (calling <see cref="Complete"/>/<see cref="Cancel"/> after that unlock resolves)
/// share the same in-memory state -- both run in this app's one process either way.
///
/// Holds only what's needed to finish the job: the two <see cref="AutofillId"/>s Android itself
/// already told <see cref="KeyKeeperAutofillService"/> about (so this class never re-derives them
/// from a raw <c>AssistStructure</c> it doesn't have) and the matched request host string. Never
/// holds any vault data -- <see cref="Complete"/> only reads the vault (via <see cref="
/// IVaultItemRepository"/>) after confirming <see cref="IAuthenticationService.IsUnlocked"/> is
/// true, at the moment it actually needs to, not before.
/// </summary>
public sealed class AutofillCompletionHandler : IAutofillCompletionHandler
{
    private readonly IAuthenticationService _auth;
    private readonly IVaultItemRepository _repository;

    private AutofillId? _usernameId;
    private AutofillId? _passwordId;
    private string? _requestHost;

    public AutofillCompletionHandler(IAuthenticationService auth, IVaultItemRepository repository)
    {
        _auth = auth;
        _repository = repository;
    }

    public bool HasPendingRequest => _usernameId is not null || _passwordId is not null;

    /// <summary>Called by <see cref="MainActivity"/> when it's launched via the authentication
    /// <c>PendingIntent</c> -- see <see cref="KeyKeeperAutofillService.OnFillRequest"/>.</summary>
    public void SetPendingRequest(AutofillId? usernameId, AutofillId? passwordId, string? requestHost)
    {
        _usernameId = usernameId;
        _passwordId = passwordId;
        _requestHost = requestHost;
    }

    [SupportedOSPlatform("android26.0")]
    public void Complete()
    {
        Activity? activity = Platform.CurrentActivity;
        try
        {
            // Defensive, not expected in practice: this is only ever called right after LockPage's
            // own Unlocked event fires, so IsUnlocked should always be true here. If it somehow
            // isn't, fall through to Cancel semantics (no result) rather than ever reading the
            // repository against a locked vault.
            if (activity is null || !_auth.IsUnlocked)
            {
                activity?.SetResult(Result.Canceled);
                return;
            }

            FillResponse? response = AutofillResponseBuilder.Build(_repository, _requestHost, _usernameId, _passwordId);
            if (response is null)
            {
                activity.SetResult(Result.Canceled);
                return;
            }

            var resultIntent = new Intent();
            resultIntent.PutExtra(AutofillManager.ExtraAuthenticationResult, response);
            activity.SetResult(Result.Ok, resultIntent);
        }
        finally
        {
            Clear();
            activity?.Finish();
        }
    }

    public void Cancel() => Clear();

    private void Clear()
    {
        _usernameId = null;
        _passwordId = null;
        _requestHost = null;
    }
}
