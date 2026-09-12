using KeyKeeper.Core.Auth;
using KeyKeeper.Core.Crypto;
using KeyKeeper.Core.Data;
using KeyKeeper.Core.Storage;
using KeyKeeper.Services;
using KeyKeeper.ViewModels;
using KeyKeeper.Views;
using Microsoft.Extensions.Logging;
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

            // KeyKeeper.Services.BiometricAuthService: two separate classes, one per platform,
            // both under this same namespace -- Platforms/Android/BiometricAuthService.cs (built
            // on AndroidX.Biometric) and Platforms/iOS/BiometricAuthService.cs (built on
            // LocalAuthentication). The MAUI SDK only compiles each Platforms/<X>/ folder into
            // its matching TargetFramework, so exactly one of the two is ever part of a given
            // build -- this single registration resolves to whichever one that is.
            builder.Services.AddSingleton<IBiometricAuthService, BiometricAuthService>();
            builder.Services.AddSingleton<IAuthenticationService, AuthenticationService>();

            // Pages/ViewModels: transient, not singleton -- each navigation should get a fresh
            // ViewModel (e.g. LockPage's Password field shouldn't survive to the next time the
            // user lands back on it after a lock). Pages take their ViewModel via constructor
            // injection and set it as BindingContext themselves (see each page's code-behind).
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

            return builder.Build();
        }
    }
}
