using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeyKeeper.Core.Data;
using KeyKeeper.Core.Models;

namespace KeyKeeper.ViewModels;

public sealed partial class LicenseFormViewModel : VaultItemFormViewModelBase<LicenseItem>
{
    public LicenseFormViewModel(IVaultItemRepository repository, IFolderRepository folders) : base(repository, folders)
    {
    }

    [ObservableProperty]
    private string title = string.Empty;

    [ObservableProperty]
    private string? licenseKey;

    [ObservableProperty]
    private string? softwareName;

    /// <summary><c>DatePicker</c> binds to <see cref="DateTime"/>, not the payload's
    /// <see cref="DateTimeOffset"/>? — kept separate from <see cref="HasExpiry"/> so an expiry
    /// date can be typed/picked and then temporarily hidden without losing it.</summary>
    [ObservableProperty]
    private DateTime expiryDate = DateTime.Today;

    [ObservableProperty]
    private bool hasExpiry;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLicenseKeyMasked))]
    private bool isLicenseKeyVisible;

    public bool IsLicenseKeyMasked => !IsLicenseKeyVisible;

    [RelayCommand]
    private void ToggleLicenseKeyVisibility() => IsLicenseKeyVisible = !IsLicenseKeyVisible;

    protected override LicenseItem BuildPayload() => new(
        Title,
        LicenseKey,
        SoftwareName,
        HasExpiry ? new DateTimeOffset(DateOnly.FromDateTime(ExpiryDate).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero) : null);

    protected override void LoadFromPayload(LicenseItem payload)
    {
        Title = payload.Title;
        LicenseKey = payload.LicenseKey;
        SoftwareName = payload.SoftwareName;
        HasExpiry = payload.ExpiresAtUtc is not null;
        ExpiryDate = payload.ExpiresAtUtc?.UtcDateTime ?? DateTime.Today;
    }
}
