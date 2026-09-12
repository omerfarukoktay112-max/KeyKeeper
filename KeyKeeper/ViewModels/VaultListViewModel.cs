using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeyKeeper.Core.Auth;
using KeyKeeper.Core.Data;
using KeyKeeper.Core.Models;

namespace KeyKeeper.ViewModels;

/// <summary>
/// The "//vault" root page's ViewModel — Phase 4's real vault content (a type-filtered-free list
/// of every item, an add menu, tap-to-open) plus the two lock-lifecycle responsibilities the
/// Phase 3 placeholder this replaces already had: locking the vault, and opting into biometric
/// unlock (needed for <c>LockViewModel</c>'s biometric button to ever have a stored key to use —
/// see <see cref="IAuthenticationService.EnableBiometricUnlockAsync"/>). Navigating back to the
/// lock screen on lock isn't done from here — it happens centrally wherever
/// <see cref="IAuthenticationService.Locked"/> is subscribed (see <c>App.xaml.cs</c>).
///
/// No search, no folder/tag filtering UI, no favorites toggle here — all explicitly out of scope
/// for this phase (see ARCHITECTURE.md's Phase 4 "out of scope" list). <see cref="IVaultItemRepository"/>
/// already supports favorite/folder/tag CRUD; only the UI for them is deferred.
/// </summary>
public sealed partial class VaultListViewModel : ObservableObject
{
    private readonly IVaultItemRepository _repository;
    private readonly IAuthenticationService _auth;
    private readonly IBiometricAuthService _biometrics;

    public VaultListViewModel(IVaultItemRepository repository, IAuthenticationService auth, IBiometricAuthService biometrics)
    {
        _repository = repository;
        _auth = auth;
        _biometrics = biometrics;
    }

    public ObservableCollection<VaultItemListEntry> Items { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasItems))]
    private bool isEmpty;

    public bool HasItems => !IsEmpty;

    [ObservableProperty]
    private bool isBiometricAvailable;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBiometricStatusMessage))]
    private string? biometricStatusMessage;

    public bool HasBiometricStatusMessage => !string.IsNullOrEmpty(BiometricStatusMessage);

    /// <summary>Call from the page's <c>OnAppearing</c> — refreshes both the biometric-button
    /// visibility and the item list every time the page becomes visible again (including
    /// returning from a form page after a create/edit/delete), not just the first time.</summary>
    public async Task InitializeAsync()
    {
        BiometricAvailability availability = await _biometrics.CheckAvailabilityAsync();
        IsBiometricAvailable = availability == BiometricAvailability.Available;
        Load();
    }

    public void Load()
    {
        Items.Clear();
        foreach (VaultItemRecord record in _repository.List())
        {
            Items.Add(VaultItemListEntry.From(record));
        }

        IsEmpty = Items.Count == 0;
    }

    [RelayCommand]
    private void Lock() => _auth.Lock();

    [RelayCommand]
    private async Task EnableBiometricUnlockAsync()
    {
        await _auth.EnableBiometricUnlockAsync();
        BiometricStatusMessage = "Biyometrik kilit açma etkinleştirildi.";
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        string choice = await Shell.Current.DisplayActionSheetAsync(
            "Yeni öğe", "İptal", null, "Giriş Bilgisi", "Kart", "WiFi", "Güvenli Not", "Lisans");

        string? route = RouteFor(choice);
        if (route is not null)
        {
            await Shell.Current.GoToAsync(route);
        }
    }

    [RelayCommand]
    private async Task OpenAsync(VaultItemListEntry entry)
    {
        string route = RouteForType(entry.ItemType);
        await Shell.Current.GoToAsync($"{route}?id={entry.Id}");
    }

    private static string? RouteFor(string addMenuChoice) => addMenuChoice switch
    {
        "Giriş Bilgisi" => "login-form",
        "Kart" => "card-form",
        "WiFi" => "wifi-form",
        "Güvenli Not" => "note-form",
        "Lisans" => "license-form",
        _ => null,
    };

    private static string RouteForType(VaultItemType type) => type switch
    {
        VaultItemType.Login => "login-form",
        VaultItemType.Card => "card-form",
        VaultItemType.WiFi => "wifi-form",
        VaultItemType.Note => "note-form",
        VaultItemType.License => "license-form",
        _ => throw new InvalidOperationException($"No form route registered for item type '{type}'."),
    };
}
