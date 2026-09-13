using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Views.Autofill;
using KeyKeeper.Core.Autofill;
using KeyKeeper.Services;

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

            HandleAutofillAuthIntent(Intent);
        }

        protected override void OnNewIntent(Intent? intent)
        {
            base.OnNewIntent(intent);
            HandleAutofillAuthIntent(intent);
        }

        /// <summary>Phase 10: recognizes a launch that came from <see cref="KeyKeeperAutofillService"/>'s
        /// authentication <c>PendingIntent</c> (tapped while the vault was locked) and records the
        /// pending request on the shared <see cref="IAutofillCompletionHandler"/> singleton --
        /// <c>LockPage.OnUnlocked</c> checks it once the user actually unlocks, normally, through
        /// this same Activity's ordinary lock-screen navigation (nothing here bypasses that). Both
        /// <c>OnCreate</c> (cold start) and <c>OnNewIntent</c> (this Activity already on top --
        /// <c>LaunchMode.SingleTop</c>) need this: which one fires depends on whether the task was
        /// already running when the PendingIntent's Activity launch happened.</summary>
        private void HandleAutofillAuthIntent(Intent? intent)
        {
            if (intent?.Action != AutofillAuthContract.Action)
            {
                return;
            }

            var usernameId = intent.GetParcelableExtra(AutofillAuthContract.ExtraUsernameId) as AutofillId;
            var passwordId = intent.GetParcelableExtra(AutofillAuthContract.ExtraPasswordId) as AutofillId;
            string? requestHost = intent.GetStringExtra(AutofillAuthContract.ExtraRequestHost);

            var completion = IPlatformApplication.Current?.Services.GetService(typeof(IAutofillCompletionHandler))
                as AutofillCompletionHandler;
            completion?.SetPendingRequest(usernameId, passwordId, requestHost);
        }
    }
}
