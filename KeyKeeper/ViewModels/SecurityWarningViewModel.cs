using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeyKeeper.Core.Auth;
using KeyKeeper.Core.Security;

namespace KeyKeeper.ViewModels;

/// <summary>
/// Thin wrapper around <see cref="IDeviceSecurityWarningService"/> (the actually-tested piece —
/// see <c>DeviceSecurityWarningServiceTests</c>): builds a plain-language Turkish message from
/// <see cref="IDeviceSecurityWarningService.PendingWarning"/> (never dumping the raw
/// <c>Signals</c> list to the user) and turns "continue anyway" into acknowledging the warning and
/// navigating to whatever route <see cref="AppRouting.DetermineInitialRoute"/> would have picked at
/// launch. Deliberately offers no "close app" action — see ARCHITECTURE.md's Phase 7 section for
/// why a forced close was rejected in favor of a dismissable warning.
/// </summary>
public sealed partial class SecurityWarningViewModel : ObservableObject
{
    private readonly IDeviceSecurityWarningService _securityWarning;
    private readonly IAuthenticationService _auth;

    public SecurityWarningViewModel(IDeviceSecurityWarningService securityWarning, IAuthenticationService auth)
    {
        _securityWarning = securityWarning;
        _auth = auth;
    }

    [ObservableProperty]
    private string message = string.Empty;

    /// <summary>Raised once the user has acknowledged the warning, carrying the route the page
    /// should navigate to — kept out of this ViewModel's own responsibilities to avoid giving it a
    /// Shell/navigation dependency, same pattern as <c>LockViewModel.Unlocked</c>.</summary>
    public event Action<string>? Continued;

    /// <summary>Call from the page's <c>OnAppearing</c> — builds the message from whatever warning
    /// is currently pending (set by <c>App.xaml.cs</c> before navigating here).</summary>
    public void Initialize()
    {
        DeviceSecurityCheckResult? warning = _securityWarning.PendingWarning;
        if (warning is null)
        {
            Message = string.Empty;
            return;
        }

        var reasons = new List<string>();
        if (warning.IsRootedOrJailbroken)
        {
            reasons.Add("cihazınızın root/jailbreak yapılmış olabileceğine dair belirtiler");
        }

        if (warning.IsDebuggerAttached)
        {
            reasons.Add("uygulamaya bağlı bir hata ayıklayıcı (debugger)");
        }

        Message =
            $"Bu cihazda {string.Join(" ve ", reasons)} tespit edildi. Bu durum, kasanızın " +
            "dayandığı işletim sistemi güvenlik korumalarını (ör. donanım destekli anahtar " +
            "depolama) zayıflatmış olabilir. Devam etmek size kalmıştır.";
    }

    [RelayCommand]
    private void Continue()
    {
        _securityWarning.AcknowledgeAndContinue();
        Continued?.Invoke(AppRouting.DetermineInitialRoute(_auth));
    }
}
