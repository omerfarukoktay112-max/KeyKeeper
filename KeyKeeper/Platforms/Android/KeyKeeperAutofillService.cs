using System.Runtime.Versioning;
using Android.App;
using Android.App.Assist;
using Android.Content;
using Android.OS;
using Android.Service.Autofill;
using Android.Text;
using Android.Views;
using Android.Views.Autofill;
using Android.Widget;
using KeyKeeper.Core.Auth;
using KeyKeeper.Core.Autofill;
using KeyKeeper.Core.Data;

namespace KeyKeeper.Services;

/// <summary>
/// Android's autofill entry point (Phase 10) -- registered via the <see cref="ServiceAttribute"/>
/// below (the same "declare via a C# attribute, let the build merge it into AndroidManifest.xml"
/// mechanism <c>MainActivity</c>'s <c>[Activity]</c> already uses; see that class and Phase 3's
/// manifest-merge notes in ARCHITECTURE.md -- nothing needs hand-editing AndroidManifest.xml
/// directly). <c>android:permission="BIND_AUTOFILL_SERVICE"</c> is what lets only the OS itself
/// bind to this service; no app, including this one, can bind to it directly.
///
/// <b>Security invariant this whole class is built around</b>: <see cref="OnFillRequest"/> never
/// reads a single field of vault data while <see cref="IAuthenticationService.IsUnlocked"/> is
/// false. When locked, it builds a response whose only action is "open KeyKeeper's own lock
/// screen" (<see cref="IAuthenticationService"/>'s normal master-password/biometric unlock, not a
/// separate credential prompt of its own) -- the real credentials are derived only after that
/// unlock actually succeeds, by <see cref="AutofillCompletionHandler.Complete"/>, which is the
/// only other place in this app that calls <see cref="AutofillResponseBuilder.Build"/>. Resolves
/// its dependencies from <see cref="IPlatformApplication.Current"/>'s service provider rather than
/// constructor injection -- like <c>PageActivityBehavior</c>, this class is instantiated by the
/// Android OS itself, outside this app's DI container, so there is no constructor for it to inject
/// into.
/// </summary>
[Service(Permission = "android.permission.BIND_AUTOFILL_SERVICE", Exported = true, Label = "KeyKeeper")]
[IntentFilter(["android.service.autofill.AutofillService"])]
[SupportedOSPlatform("android26.0")]
public sealed class KeyKeeperAutofillService : AutofillService
{
    private static int _pendingIntentRequestCode;

    public override void OnFillRequest(FillRequest request, CancellationSignal cancellationSignal, FillCallback callback)
    {
        AssistStructure? structure = request.FillContexts?.Count > 0
            ? request.FillContexts[^1].Structure
            : null;
        if (structure is null)
        {
            callback.OnSuccess(null);
            return;
        }

        var fields = new List<(AutofillId Id, FieldKind Kind)>();
        string? webDomain = null;
        CollectFields(structure, fields, ref webDomain);

        AutofillId? usernameId = fields.FirstOrDefault(f => f.Kind == FieldKind.Username).Id;
        AutofillId? passwordId = fields.FirstOrDefault(f => f.Kind == FieldKind.Password).Id;

        // Native (non-browser) apps report no web domain at all -- this phase only matches saved
        // logins by URL/web-domain (see AutofillDomainMatcher's doc comment), so there's nothing
        // to offer without one, regardless of whether autofillable fields were found.
        if (webDomain is null || (usernameId is null && passwordId is null))
        {
            callback.OnSuccess(null);
            return;
        }

        IServiceProvider? services = IPlatformApplication.Current?.Services;
        IAuthenticationService? auth = services?.GetService(typeof(IAuthenticationService)) as IAuthenticationService;
        IVaultItemRepository? repository = services?.GetService(typeof(IVaultItemRepository)) as IVaultItemRepository;
        if (auth is null || repository is null)
        {
            callback.OnSuccess(null);
            return;
        }

        if (auth.IsUnlocked)
        {
            FillResponse? unlockedResponse = AutofillResponseBuilder.Build(repository, webDomain, usernameId, passwordId);
            callback.OnSuccess(unlockedResponse);
            return;
        }

        callback.OnSuccess(BuildAuthenticationRequiredResponse(fields, usernameId, passwordId, webDomain));
    }

    public override void OnSaveRequest(SaveRequest request, SaveCallback callback)
    {
        // Phase 10 deliberately never calls FillResponse.Builder.SetSaveInfo(...) in
        // OnFillRequest, so Android has no reason to ever actually invoke this callback -- "offer
        // to save a newly typed login from another app into KeyKeeper" is a reasonable future
        // enhancement (see ARCHITECTURE.md's Phase 10 "Explicitly out of scope" note), not
        // attempted this phase. Implemented only to satisfy AutofillService's abstract contract.
        callback.OnSuccess();
    }

    private FillResponse BuildAuthenticationRequiredResponse(
        List<(AutofillId Id, FieldKind Kind)> fields, AutofillId? usernameId, AutofillId? passwordId, string webDomain)
    {
        var authIntent = new Intent(this, typeof(MainActivity));
        authIntent.SetAction(AutofillAuthContract.Action);
        if (usernameId is not null)
        {
            authIntent.PutExtra(AutofillAuthContract.ExtraUsernameId, usernameId);
        }

        if (passwordId is not null)
        {
            authIntent.PutExtra(AutofillAuthContract.ExtraPasswordId, passwordId);
        }

        authIntent.PutExtra(AutofillAuthContract.ExtraRequestHost, webDomain);

        PendingIntent pendingIntent = PendingIntent.GetActivity(
            this,
            System.Threading.Interlocked.Increment(ref _pendingIntentRequestCode),
            authIntent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)!;

        AutofillId[] authIds = fields.Select(f => f.Id).ToArray();
        RemoteViews presentation = AutofillResponseBuilder.BuildPresentation("KeyKeeper ile aç 🔒");

        return new FillResponse.Builder()
            .SetAuthentication(authIds, pendingIntent.IntentSender, presentation)
            .Build()!;
    }

    private enum FieldKind { Other, Username, Password }

    private static void CollectFields(AssistStructure structure, List<(AutofillId Id, FieldKind Kind)> fields, ref string? webDomain)
    {
        int windowCount = structure.WindowNodeCount;
        for (int i = 0; i < windowCount; i++)
        {
            AssistStructure.ViewNode? root = structure.GetWindowNodeAt(i)?.RootViewNode;
            if (root is not null)
            {
                Visit(root, fields, ref webDomain);
            }
        }
    }

    private static void Visit(AssistStructure.ViewNode node, List<(AutofillId Id, FieldKind Kind)> fields, ref string? webDomain)
    {
        webDomain ??= node.WebDomain;

        AutofillId? id = node.AutofillId;
        if (id is not null)
        {
            FieldKind kind = ClassifyNode(node);
            if (kind != FieldKind.Other)
            {
                fields.Add((id, kind));
            }
        }

        int childCount = node.ChildCount;
        for (int i = 0; i < childCount; i++)
        {
            AssistStructure.ViewNode? child = node.GetChildAt(i);
            if (child is not null)
            {
                Visit(child, fields, ref webDomain);
            }
        }
    }

    /// <summary>Explicit <c>View</c> autofill hints first (what a well-annotated web form or
    /// native app declares); falling back to the raw Android input-type flags (common on plain
    /// <c>&lt;input&gt;</c> fields a page never annotated) when no hint is present. A best-effort
    /// heuristic, not exhaustive -- same "documented, tunable" spirit as <c>Argon2Constants</c>/
    /// <c>LockoutPolicy</c>, not a claim every possible login form is recognized.</summary>
    private static FieldKind ClassifyNode(AssistStructure.ViewNode node)
    {
        string[]? hints = node.GetAutofillHints();
        if (hints is not null)
        {
            if (hints.Contains(global::Android.Views.View.AutofillHintPassword))
            {
                return FieldKind.Password;
            }

            if (hints.Contains(global::Android.Views.View.AutofillHintUsername) || hints.Contains(global::Android.Views.View.AutofillHintEmailAddress))
            {
                return FieldKind.Username;
            }
        }

        var inputType = (InputTypes)node.InputType;
        InputTypes variation = inputType & InputTypes.MaskVariation;

        if (variation is InputTypes.TextVariationPassword or InputTypes.TextVariationWebPassword or InputTypes.TextVariationVisiblePassword)
        {
            return FieldKind.Password;
        }

        if (variation is InputTypes.TextVariationEmailAddress or InputTypes.TextVariationWebEmailAddress)
        {
            return FieldKind.Username;
        }

        return FieldKind.Other;
    }
}
