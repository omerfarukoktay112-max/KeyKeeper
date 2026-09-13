using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeyKeeper.Core.Autofill;

namespace KeyKeeper.ViewModels;

/// <summary>
/// Phase 10: currently just the autofill-service status indicator + "open system settings"
/// button — see <see cref="IAutofillServiceStatusChecker"/> for why this can only report status
/// and open the OS picker, never select itself as the active service. A MAUI app cannot request
/// biometric/master-password re-authentication here beyond what's already required to reach this
/// page (the vault must already be unlocked to navigate to it from <c>VaultListPage</c>) — nothing
/// on this page reads or writes vault data.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly IAutofillServiceStatusChecker _autofillStatus;

    public SettingsViewModel(IAutofillServiceStatusChecker autofillStatus)
    {
        _autofillStatus = autofillStatus;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AutofillStatusMessage))]
    private bool isAutofillEnabled;

    public string AutofillStatusMessage => IsAutofillEnabled
        ? "KeyKeeper şu anda sistemin otomatik doldurma servisi olarak seçili."
        : "KeyKeeper şu anda otomatik doldurma servisi olarak seçili değil.";

    /// <summary>Call from <c>OnAppearing</c> — the user may have changed this in system settings
    /// and come straight back, same "refresh on every appear, not just the first" reasoning as
    /// every other status-on-appear check in this app (e.g. <c>LockPage</c>'s biometric-button
    /// visibility).</summary>
    public void Refresh() => IsAutofillEnabled = _autofillStatus.IsEnabled();

    [RelayCommand]
    private void OpenAutofillSettings() => _autofillStatus.OpenSettings();
}
