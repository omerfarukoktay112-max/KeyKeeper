using Android.Content;
using Android.Provider;
using Android.Views.Autofill;
using KeyKeeper.Core.Autofill;

namespace KeyKeeper.Services;

/// <summary>
/// Android's Autofill Framework (and the whole <c>android.service.autofill</c>/
/// <c>android.views.autofill</c> API surface <see cref="KeyKeeperAutofillService"/> depends on)
/// only exists from API 26 (Android 8.0) onward -- this app's <c>SupportedOSPlatformVersion</c> is
/// 23 (see ARCHITECTURE.md's "Target frameworks" section, kept at 23 for biometric unlock, not
/// autofill), so every member here is guarded by an explicit runtime SDK check rather than assumed.
/// Below API 26, <see cref="IsEnabled"/> is always false and <see cref="OpenSettings"/> is a no-op
/// -- there is no autofill settings screen to open on those OS versions.
/// </summary>
public sealed class AutofillServiceStatusChecker : IAutofillServiceStatusChecker
{
    public bool IsEnabled()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            return false;
        }

        Context context = global::Android.App.Application.Context;

        // Context.GetSystemService(string) needs the exact platform service-name constant, which
        // this binding doesn't expose as a named field on Context for every service -- the
        // Java.Lang.Class overload sidesteps that entirely and is guaranteed correct regardless.
        var manager = context.GetSystemService(Java.Lang.Class.FromType(typeof(AutofillManager))) as AutofillManager;
        return manager?.HasEnabledAutofillServices == true;
    }

    public void OpenSettings()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            return;
        }

        Context context = global::Android.App.Application.Context;

        // Settings.ActionRequestSetAutofillService, scoped to this app's own package via the
        // "package:" data URI, is the documented way to send the user straight to the system
        // picker for this specific app -- not the generic autofill settings list, which would
        // leave the user to find KeyKeeper themselves among every installed autofill provider.
        var intent = new Intent(Settings.ActionRequestSetAutofillService);
        intent.SetData(global::Android.Net.Uri.Parse("package:" + context.PackageName));
        intent.AddFlags(ActivityFlags.NewTask);
        context.StartActivity(intent);
    }
}
