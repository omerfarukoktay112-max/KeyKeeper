using KeyKeeper.Core.Auth;

namespace KeyKeeper
{
    public partial class App : Application
    {
        private readonly IAuthenticationService _auth;

        public App(IAuthenticationService auth)
        {
            InitializeComponent();
            _auth = auth;

            // The single place deciding "go to the lock screen" for every path that can lock
            // the vault -- the explicit "Kilitle" button on VaultPlaceholderPage and the
            // inactivity auto-lock timer both end up here via AuthenticationService.Lock()
            // raising this event (see IAuthenticationService.Locked's doc comment).
            _auth.Locked += OnVaultLocked;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window(new AppShell());
            window.Created += async (_, _) => await NavigateToInitialRouteAsync();
            return window;
        }

        private async Task NavigateToInitialRouteAsync()
        {
            string route = !_auth.IsVaultInitialized
                ? "//setup"
                : _auth.IsUnlocked
                    ? "//vault"
                    : "//lock";

            if (Shell.Current is not null)
            {
                await Shell.Current.GoToAsync(route);
            }
        }

        private void OnVaultLocked()
        {
            // Raised from AuthenticationService.Lock(). For the inactivity auto-lock timer this
            // fires on a background thread, not the UI thread -- unlike the button-triggered
            // ViewModel commands elsewhere in this app, which already resume on the UI thread
            // via MAUI's SynchronizationContext after their awaits, so this is the one place
            // that actually needs an explicit marshal.
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                if (Shell.Current is not null)
                {
                    await Shell.Current.GoToAsync("//lock");
                }
            });
        }
    }
}
