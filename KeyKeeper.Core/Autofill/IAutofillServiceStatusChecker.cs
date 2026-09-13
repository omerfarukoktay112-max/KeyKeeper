namespace KeyKeeper.Core.Autofill;

/// <summary>
/// Backs the Settings page's "is KeyKeeper the active autofill service" indicator and its
/// "open system autofill settings" button (Phase 10). A MAUI app cannot make itself the OS's
/// default autofill service programmatically on either platform — the user must choose it
/// themselves in system settings; this only reports current status and opens that settings screen.
///
/// The iOS implementation always reports <see cref="IsEnabled"/> <c>false</c> and
/// <see cref="OpenSettings"/> as a no-op — iOS autofill (a Credential Provider Extension) is a
/// different mechanism entirely and out of scope for Phase 10; see ARCHITECTURE.md.
/// </summary>
public interface IAutofillServiceStatusChecker
{
    /// <summary>True if KeyKeeper is currently the OS's selected autofill service. On Android,
    /// false on API levels below 26 (the Autofill Framework doesn't exist there) as well as when
    /// genuinely not selected.</summary>
    bool IsEnabled();

    /// <summary>Opens the OS screen where the user picks their autofill service.</summary>
    void OpenSettings();
}
