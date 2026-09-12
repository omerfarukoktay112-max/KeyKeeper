using Foundation;
using UIKit;

namespace KeyKeeper.Services;

/// <summary>
/// iOS has no FLAG_SECURE equivalent -- no API blocks screen recording outright, and the
/// app-switcher preview is just a snapshot of whatever the window looked like when the app
/// resigned active. Instead, this covers the entire key window with a blur + logo overlay
/// whenever either condition that could expose vault content applies:
///
/// - <see cref="UIScreen.CapturedDidChangeNotification"/> fires when screen recording/mirroring
///   starts or stops (<see cref="UIScreen.Captured"/>).
/// - The app is about to resign active (<c>applicationWillResignActive</c>/<c>DidBecomeActive</c>,
///   wired from <c>MauiProgram.cs</c> via <c>ConfigureLifecycleEvents</c>) -- covers both a real
///   backgrounding (the app-switcher snapshot is taken at exactly this point) and transient
///   inactivity (an incoming call, Control Center), the same technique widely used by banking/
///   password-manager apps.
///
/// A single central service (started once at launch), not per-page code -- mirrors Android's
/// MainActivity.OnCreate FLAG_SECURE being one call that covers every page automatically. Unlike
/// FLAG_SECURE, this deliberately *does* toggle (there's no equivalent of "just leave it on" for a
/// full-screen opaque overlay -- it would otherwise permanently hide the app). The two triggers are
/// tracked independently and the overlay is shown whenever either is true, hidden only once both
/// have cleared.
///
/// Unverified beyond compiling -- no Mac/Xcode available in this environment (see
/// ARCHITECTURE.md's Phase 7 section).
/// </summary>
public static class SecureOverlayService
{
    private static NSObject? _captureObserver;
    private static UIView? _overlay;
    private static bool _isBeingCaptured;
    private static bool _isResignedActive;

    /// <summary>Call once at app startup (from MauiProgram.cs's iOS FinishedLaunching lifecycle
    /// hook) to begin observing screen-capture state. Idempotent.</summary>
    public static void Start()
    {
        if (_captureObserver is not null)
        {
            return;
        }

        // UIScreen.Captured is deprecated in favor of UITraitCollection.SceneCaptureState
        // starting iOS 17.2, but that replacement requires iOS 17+ and this app's
        // SupportedOSPlatformVersion is 15.0 (see KeyKeeper.csproj) -- UIScreen.Captured remains
        // the only API that works across the app's full supported range, so the deprecation
        // warning is accepted here rather than dropping pre-17.2 support.
#pragma warning disable CA1422
        _captureObserver = NSNotificationCenter.DefaultCenter.AddObserver(
            UIScreen.CapturedDidChangeNotification,
            _ =>
            {
                _isBeingCaptured = UIScreen.MainScreen.Captured;
                UpdateOverlayVisibility();
            });

        _isBeingCaptured = UIScreen.MainScreen.Captured;
#pragma warning restore CA1422
        UpdateOverlayVisibility();
    }

    public static void SetResignedActive(bool isResignedActive)
    {
        _isResignedActive = isResignedActive;
        UpdateOverlayVisibility();
    }

    private static void UpdateOverlayVisibility()
    {
        if (_isBeingCaptured || _isResignedActive)
        {
            ShowOverlay();
        }
        else
        {
            HideOverlay();
        }
    }

    private static void ShowOverlay()
    {
        // UIApplication.Windows is deprecated in favor of UIWindowScene.Windows, which requires
        // adopting UIScene/SceneDelegate -- this app deliberately has neither (a single
        // AppDelegate-based app, see Platforms/iOS/AppDelegate.cs), so UIApplication.Windows
        // remains the correct API for this app's actual architecture.
#pragma warning disable CA1422
        UIWindow? keyWindow = UIApplication.SharedApplication.Windows.FirstOrDefault(w => w.IsKeyWindow);
#pragma warning restore CA1422
        if (keyWindow is null || _overlay is not null)
        {
            return;
        }

        var blurEffect = UIBlurEffect.FromStyle(UIBlurEffectStyle.SystemMaterialDark);
        var blurView = new UIVisualEffectView(blurEffect)
        {
            Frame = keyWindow.Bounds,
            AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight,
        };

        var label = new UILabel
        {
            Text = "KeyKeeper",
            TextColor = UIColor.White,
            Font = UIFont.BoldSystemFontOfSize(28)!,
            TextAlignment = UITextAlignment.Center,
            TranslatesAutoresizingMaskIntoConstraints = false,
        };
        blurView.ContentView.AddSubview(label);
        NSLayoutConstraint.ActivateConstraints(new[]
        {
            label.CenterXAnchor.ConstraintEqualTo(blurView.ContentView.CenterXAnchor),
            label.CenterYAnchor.ConstraintEqualTo(blurView.ContentView.CenterYAnchor),
        });

        _overlay = blurView;
        keyWindow.AddSubview(blurView);
    }

    private static void HideOverlay()
    {
        _overlay?.RemoveFromSuperview();
        _overlay = null;
    }
}
