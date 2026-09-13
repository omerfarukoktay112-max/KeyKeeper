using KeyKeeper.Core.Auth;
using KeyKeeper.Core.Autofill;
using KeyKeeper.Core.Backup;
using KeyKeeper.Core.Crypto;
using KeyKeeper.Core.Data;
using KeyKeeper.Core.Security;
using KeyKeeper.Core.Storage;
using KeyKeeper.Core.Tools;
using KeyKeeper.Services;
using CoreIClipboard = KeyKeeper.Core.Tools.IClipboard;
using KeyKeeper.ViewModels;
using KeyKeeper.Views;
using Microsoft.Extensions.Logging;
#if IOS
using Microsoft.Maui.LifecycleEvents;
#endif
using SQLitePCL;

namespace KeyKeeper
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            // Composition-root decision: this app's vault database is SQLCipher-encrypted.
            // KeyKeeper.Core.Data.VaultDatabaseService itself is provider-agnostic -- it just
            // uses sqlite-net-pcl -- so which native engine backs it is picked here, once, at
            // startup. See VaultDatabaseService's doc comment for how the derived key then
            // reaches SQLCipher.
            //
            // Android links e_sqlcipher dynamically (a .so loaded via P/Invoke), using the
            // dedicated SQLite3Provider_e_sqlcipher binding. iOS statically links the same
            // SQLCipher native library into the app binary (see the bundle's "static/device"
            // and "static/simulator" .a files) and instead uses the generic
            // SQLite3Provider_internal binding, which resolves against whatever SQLite symbols
            // are statically linked in -- SQLitePCLRaw.bundle_e_sqlcipher's own dependency
            // manifest picks provider.internal (not provider.e_sqlcipher) for iOS for exactly
            // this reason.
#if ANDROID
            raw.SetProvider(new SQLite3Provider_e_sqlcipher());
#elif IOS
            raw.SetProvider(new SQLite3Provider_internal());
#endif

            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
    		builder.Logging.AddDebug();
#endif

#if IOS
            // Phase 7: SecureOverlayService covers the whole app -- no FLAG_SECURE equivalent
            // exists on iOS, so a blur/logo overlay is shown instead whenever screen recording is
            // detected or the app is about to resign active (about to background, or a system UI
            // like Control Center is about to cover it) -- see SecureOverlayService's own doc
            // comment for why both triggers are needed and why OnResignActivation/OnActivated
            // (not DidEnterBackground/WillEnterForeground) are the right pair to hook.
            builder.ConfigureLifecycleEvents(events =>
            {
                events.AddiOS(ios => ios
                    .FinishedLaunching((_, _) =>
                    {
                        SecureOverlayService.Start();
                        return true;
                    })
                    .OnResignActivation(_ => SecureOverlayService.SetResignedActive(true))
                    .OnActivated(_ => SecureOverlayService.SetResignedActive(false)));
            });
#endif

            builder.Services.AddSingleton<IVaultStoragePathProvider, MauiVaultStoragePathProvider>();
            builder.Services.AddSingleton<IVaultMigrationService, VaultMigrationService>();
            builder.Services.AddSingleton<IVaultDatabaseService, VaultDatabaseService>();
            builder.Services.AddSingleton<ISecureKeyValueStore, MauiSecureKeyValueStore>();
            builder.Services.AddSingleton<IWrappedKeyStorage, WrappedKeyStorage>();
            builder.Services.AddSingleton<IVaultUnlockStore, VaultUnlockStore>();
            builder.Services.AddSingleton<IInactivityTimerService, InactivityTimerService>();
            builder.Services.AddSingleton<IKeyDerivationService, Argon2idKeyDerivationService>();
            builder.Services.AddSingleton<IMasterPasswordVerifier, MasterPasswordVerifier>();
            builder.Services.AddSingleton<IEncryptionService, AesGcmEncryptionService>();
            builder.Services.AddSingleton<IVaultItemRepository, VaultItemRepository>();
            builder.Services.AddSingleton<IFolderRepository, FolderRepository>();
            builder.Services.AddSingleton<ITagRepository, TagRepository>();

            // Yedekleme (Phase 8): BackupFormatMigrator/BackupService are stateless, cheap to
            // construct singletons -- same lifetime reasoning as the repositories above.
            builder.Services.AddSingleton<IBackupFormatMigrator, BackupFormatMigrator>();
            builder.Services.AddSingleton<IBackupService, BackupService>();
            builder.Services.AddSingleton<ICsvExportService, CsvExportService>();

            // Araçlar (Phase 6): pure-logic pieces are stateless/cheap (or, for the clipboard
            // clearer, need exactly one shared pending-clear timer app-wide), so all four are
            // singletons -- same lifetime reasoning as the repositories above.
            builder.Services.AddSingleton<IPasswordGenerator, PasswordGenerator>();
            builder.Services.AddSingleton<IPasswordStrengthAnalyzer, PasswordStrengthAnalyzer>();
            builder.Services.AddSingleton<ITotpGenerator, TotpGenerator>();
            builder.Services.AddSingleton<CoreIClipboard, MauiClipboard>();
            builder.Services.AddSingleton<IClipboardClearingService, ClipboardClearingService>();

            // KeyKeeper.Services.BiometricAuthService: two separate classes, one per platform,
            // both under this same namespace -- Platforms/Android/BiometricAuthService.cs (built
            // on AndroidX.Biometric) and Platforms/iOS/BiometricAuthService.cs (built on
            // LocalAuthentication). The MAUI SDK only compiles each Platforms/<X>/ folder into
            // its matching TargetFramework, so exactly one of the two is ever part of a given
            // build -- this single registration resolves to whichever one that is.
            builder.Services.AddSingleton<IBiometricAuthService, BiometricAuthService>();
            builder.Services.AddSingleton<IAuthenticationService, AuthenticationService>();

            // Güvenlik Zırhı (Phase 7): DeviceSecurityChecker follows the exact same
            // one-class-per-platform-folder pattern as BiometricAuthService above.
            // DeviceSecurityWarningService is a singleton so the one evaluation done at launch
            // (App.xaml.cs) and the acknowledgement made on SecurityWarningPage act on the same
            // in-memory state.
            builder.Services.AddSingleton<IDeviceSecurityChecker, DeviceSecurityChecker>();
            builder.Services.AddSingleton<IDeviceSecurityWarningService, DeviceSecurityWarningService>();

            // Otomatik Doldurma (Phase 10): AutofillCompletionHandler is a singleton because
            // KeyKeeperAutofillService (Android) and MainActivity write to it and LockPage reads
            // from it across separate DI-outside-the-container resolutions -- see its own doc
            // comment. IAutofillServiceStatusChecker is stateless, same lifetime reasoning as every
            // other platform-glue singleton above.
            builder.Services.AddSingleton<IAutofillCompletionHandler, AutofillCompletionHandler>();
            builder.Services.AddSingleton<IAutofillServiceStatusChecker, AutofillServiceStatusChecker>();

            // Pages/ViewModels: transient, not singleton -- each navigation should get a fresh
            // ViewModel (e.g. LockPage's Password field shouldn't survive to the next time the
            // user lands back on it after a lock). Pages take their ViewModel via constructor
            // injection and set it as BindingContext themselves (see each page's code-behind).
            builder.Services.AddTransient<SecurityWarningViewModel>();
            builder.Services.AddTransient<SecurityWarningPage>();
            builder.Services.AddTransient<SetupViewModel>();
            builder.Services.AddTransient<SetupPage>();
            builder.Services.AddTransient<LockViewModel>();
            builder.Services.AddTransient<LockPage>();
            builder.Services.AddTransient<VaultListViewModel>();
            builder.Services.AddTransient<VaultListPage>();
            builder.Services.AddTransient<LoginFormViewModel>();
            builder.Services.AddTransient<LoginFormPage>();
            builder.Services.AddTransient<CardFormViewModel>();
            builder.Services.AddTransient<CardFormPage>();
            builder.Services.AddTransient<WifiFormViewModel>();
            builder.Services.AddTransient<WifiFormPage>();
            builder.Services.AddTransient<NoteFormViewModel>();
            builder.Services.AddTransient<NoteFormPage>();
            builder.Services.AddTransient<LicenseFormViewModel>();
            builder.Services.AddTransient<LicenseFormPage>();
            builder.Services.AddTransient<FolderManagementViewModel>();
            builder.Services.AddTransient<FolderManagementPage>();
            builder.Services.AddTransient<PasswordGeneratorViewModel>();
            builder.Services.AddTransient<PasswordGeneratorPage>();
            builder.Services.AddTransient<BackupViewModel>();
            builder.Services.AddTransient<BackupPage>();
            builder.Services.AddTransient<SettingsViewModel>();
            builder.Services.AddTransient<SettingsPage>();

            return builder.Build();
        }
    }
}
