using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;

namespace KeyKeeper
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // FLAG_SECURE, for the whole app session, not toggled per lock/unlock state: it's a
            // window-level flag, so setting it once here already covers every page automatically,
            // and on Android it blocks both screenshot/screen-recording capture AND the
            // recents/app-switcher thumbnail with the same single flag. Toggling it in sync with
            // IAuthenticationService.Locked/unlock would reintroduce the kind of async
            // background-thread-vs-UI-thread race Phase 5 already hit once; blocking screenshots
            // of the lock/setup screens too costs nothing since nothing there is sensitive but
            // there's no reason to allow it either. See ARCHITECTURE.md's Phase 7 section.
            Window?.AddFlags(WindowManagerFlags.Secure);
        }
    }
}
