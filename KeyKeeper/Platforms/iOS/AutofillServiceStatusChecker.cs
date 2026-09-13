using KeyKeeper.Core.Autofill;

namespace KeyKeeper.Services;

/// <summary>
/// iOS autofill (a Credential Provider Extension) is a different mechanism entirely and out of
/// scope for Phase 10; see ARCHITECTURE.md. Permanent "not available" stub, not a real status
/// check -- <see cref="SettingsViewModel"/>'s button/indicator only has real behavior on Android.
/// </summary>
public sealed class AutofillServiceStatusChecker : IAutofillServiceStatusChecker
{
    public bool IsEnabled() => false;

    public void OpenSettings()
    {
    }
}
