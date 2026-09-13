using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeyKeeper.Core.Auth;
using KeyKeeper.Core.Data;
using KeyKeeper.Core.Models;
using KeyKeeper.Services;

namespace KeyKeeper.ViewModels;

public enum FolderFilterKind { All, Unfiled, Specific }

/// <summary>One entry in the folder filter chip row -- "Tümü" (<see cref="FolderFilterKind.All"/>,
/// always first), "Klasörsüz" (<see cref="FolderFilterKind.Unfiled"/>, always second), then every
/// real folder (<see cref="FolderFilterKind.Specific"/>), indented per <see cref="FolderTreeFlattener"/>.
/// A mutable <see cref="ObservableObject"/> (not a record), single-select sibling of
/// <see cref="TagChipViewModel"/>'s multi-select chips -- rendered the same way, via
/// <c>BindableLayout</c> rather than a second <c>CollectionView</c> on the page (two
/// <c>CollectionView</c>s on one Android page is a known MAUI rendering pitfall: the second one
/// can silently fail to render any items at all, which is exactly what happened here during
/// manual verification before this was changed).</summary>
public sealed partial class FolderFilterOptionViewModel : ObservableObject
{
    public FolderFilterKind Kind { get; }

    public string? FolderId { get; }

    public string DisplayName { get; }

    [ObservableProperty]
    private bool isSelected;

    public FolderFilterOptionViewModel(FolderFilterKind kind, string? folderId, string displayName)
    {
        Kind = kind;
        FolderId = folderId;
        DisplayName = displayName;
    }
}

/// <summary>
/// The "//vault" root page's ViewModel — a type-filtered-free list of every item (Phase 4) plus,
/// as of Phase 5, client-side filtering (folder, tag, favorites-only) and in-memory search over
/// that same already-decrypted list, alongside the two lock-lifecycle responsibilities the
/// Phase 3 placeholder this replaces already had: locking the vault, and opting into biometric
/// unlock (needed for <c>LockViewModel</c>'s biometric button to ever have a stored key to use —
/// see <see cref="IAuthenticationService.EnableBiometricUnlockAsync"/>). Navigating back to the
/// lock screen on lock isn't done from here — it happens centrally wherever
/// <see cref="IAuthenticationService.Locked"/> is subscribed (see <c>App.xaml.cs</c>).
///
/// Filtering design (Phase 5): <see cref="IVaultItemRepository"/> exposes <c>ListByFolder</c>/
/// <c>ListByTag</c>/<c>ListFavorites</c> as independently correct, independently tested repository
/// primitives (see its tests), but this ViewModel does not call them per filter change. Folder,
/// tag(s), favorites-only, and search text can all be active simultaneously (AND'ed together,
/// tags among themselves OR'ed), which a single repository query per criterion can't express
/// without either a combinatorial method explosion or a bespoke query-object API neither this
/// phase's scope nor its "extend the repository with a few filtering methods" ask called for.
/// Loading the full list once (as Phase 4 already did) and filtering the in-memory
/// <see cref="VaultItemListEntry"/> collection client-side is also exactly what in-memory search
/// already requires, so both needs are served by one mechanism, in <see cref="ApplyFilters"/>.
/// </summary>
public sealed partial class VaultListViewModel : ObservableObject
{
    private readonly IVaultItemRepository _repository;
    private readonly IFolderRepository _folders;
    private readonly ITagRepository _tags;
    private readonly IAuthenticationService _auth;
    private readonly IBiometricAuthService _biometrics;
    private List<VaultItemListEntry> _allItems = [];

    public VaultListViewModel(
        IVaultItemRepository repository,
        IFolderRepository folders,
        ITagRepository tags,
        IAuthenticationService auth,
        IBiometricAuthService biometrics)
    {
        _repository = repository;
        _folders = folders;
        _tags = tags;
        _auth = auth;
        _biometrics = biometrics;
    }

    public ObservableCollection<VaultItemListEntry> Items { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasItems))]
    private bool isEmpty;

    public bool HasItems => !IsEmpty;

    /// <summary>Phase 9: a distinct, actionable empty state instead of one static "henüz öğe yok"
    /// message for every reason <see cref="Items"/> could be empty — an unfiltered empty vault, a
    /// search with no matches, and an empty folder/tag/favorites filter each read differently, and
    /// only the first of those is something a button on this page can actually fix.</summary>
    public string EmptyStateMessage
    {
        get
        {
            if (_allItems.Count == 0)
            {
                return "Henüz öğe yok. Başlamak için ilk öğeni ekle.";
            }

            string query = SearchText?.Trim() ?? string.Empty;
            if (query.Length > 0)
            {
                return $"\"{query}\" için sonuç bulunamadı.";
            }

            return _selectedFolderFilter?.Kind switch
            {
                FolderFilterKind.Specific => "Bu klasörde henüz öğe yok.",
                FolderFilterKind.Unfiled => "Klasörsüz öğe yok.",
                _ => "Bu filtrelerle eşleşen öğe yok.",
            };
        }
    }

    /// <summary>Only the "vault is genuinely empty" case gets a call to action -- a search or
    /// folder/tag/favorites filter that happens to match nothing isn't fixed by adding a new item,
    /// it's fixed by changing or clearing the filter.</summary>
    public bool ShowAddFirstItemButton => IsEmpty && _allItems.Count == 0;

    [ObservableProperty]
    private bool isBiometricAvailable;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBiometricStatusMessage))]
    private string? biometricStatusMessage;

    public bool HasBiometricStatusMessage => !string.IsNullOrEmpty(BiometricStatusMessage);

    public ObservableCollection<FolderFilterOptionViewModel> FolderFilterOptions { get; } = [];

    private FolderFilterOptionViewModel? _selectedFolderFilter;

    public ObservableCollection<TagChipViewModel> TagChips { get; } = [];

    [ObservableProperty]
    private bool favoritesOnly;

    [ObservableProperty]
    private string? searchText;

    /// <summary>Call from the page's <c>OnAppearing</c> — refreshes the biometric-button
    /// visibility, the folder/tag filter options, and the item list every time the page becomes
    /// visible again (including returning from a form page after a create/edit/delete), not just
    /// the first time.</summary>
    public async Task InitializeAsync()
    {
        BiometricAvailability availability = await _biometrics.CheckAvailabilityAsync();
        IsBiometricAvailable = availability == BiometricAvailability.Available;
        Load();
    }

    /// <summary>Guarded the same way as <c>FolderManagementViewModel</c>'s commands (see its
    /// <c>TryRun</c> doc comment): the vault can auto-lock between this page appearing and its
    /// data actually loading, or between a row's favorite star being tapped and
    /// <see cref="IVaultItemRepository.SetFavorite"/> running -- a locked-vault failure here
    /// should just no-op (the central <c>IAuthenticationService.Locked</c> subscriber in
    /// <c>App.xaml.cs</c> is already about to navigate to the lock screen), not crash the app.</summary>
    public void Load()
    {
        try
        {
            LoadFolderFilterOptions();
            LoadTagChips();

            _allItems = _repository.List().Select(VaultItemListEntry.From).ToList();
            ApplyFilters();
        }
        catch (Exception ex) when (ex is InvalidOperationException or VaultDatabaseException)
        {
            // The vault locked out from under this call; App.xaml.cs's Locked subscriber is
            // already navigating to the lock screen, so there's nothing more to do here.
        }
    }

    private void LoadFolderFilterOptions()
    {
        string? previouslySelectedId = _selectedFolderFilter?.FolderId;
        FolderFilterKind previouslySelectedKind = _selectedFolderFilter?.Kind ?? FolderFilterKind.All;

        FolderFilterOptions.Clear();
        FolderFilterOptions.Add(new FolderFilterOptionViewModel(FolderFilterKind.All, null, "Tümü"));
        FolderFilterOptions.Add(new FolderFilterOptionViewModel(FolderFilterKind.Unfiled, null, "Klasörsüz"));
        foreach (IndentedFolder folder in FolderTreeFlattener.Flatten(_folders.ListAll()))
        {
            FolderFilterOptions.Add(new FolderFilterOptionViewModel(FolderFilterKind.Specific, folder.Id, folder.DisplayName));
        }

        FolderFilterOptionViewModel toSelect = FolderFilterOptions.FirstOrDefault(o =>
            o.Kind == previouslySelectedKind && o.FolderId == previouslySelectedId) ?? FolderFilterOptions[0];
        toSelect.IsSelected = true;
        _selectedFolderFilter = toSelect;
    }

    private void LoadTagChips()
    {
        HashSet<string> previouslySelectedIds = TagChips.Where(c => c.IsSelected).Select(c => c.Id).ToHashSet();

        TagChips.Clear();
        foreach (TagRecord tag in _tags.ListAll())
        {
            TagChips.Add(new TagChipViewModel(tag.Id, tag.Name) { IsSelected = previouslySelectedIds.Contains(tag.Id) });
        }
    }

    partial void OnFavoritesOnlyChanged(bool value) => ApplyFilters();

    partial void OnSearchTextChanged(string? value) => ApplyFilters();

    private void ApplyFilters()
    {
        HashSet<string> selectedTagNames = TagChips.Where(c => c.IsSelected).Select(c => c.Name).ToHashSet(StringComparer.Ordinal);
        string query = SearchText?.Trim() ?? string.Empty;

        IEnumerable<VaultItemListEntry> filtered = _allItems.Where(MatchesFolder);

        if (FavoritesOnly)
        {
            filtered = filtered.Where(e => e.IsFavorite);
        }

        if (selectedTagNames.Count > 0)
        {
            filtered = filtered.Where(e => e.Tags.Any(selectedTagNames.Contains));
        }

        if (query.Length > 0)
        {
            filtered = filtered.Where(e => e.SearchText.Contains(query, StringComparison.CurrentCultureIgnoreCase));
        }

        Items.Clear();
        foreach (VaultItemListEntry entry in filtered)
        {
            Items.Add(entry);
        }

        IsEmpty = Items.Count == 0;
        OnPropertyChanged(nameof(EmptyStateMessage));
        OnPropertyChanged(nameof(ShowAddFirstItemButton));
    }

    private bool MatchesFolder(VaultItemListEntry entry) => _selectedFolderFilter?.Kind switch
    {
        FolderFilterKind.Unfiled => entry.FolderId is null,
        FolderFilterKind.Specific => entry.FolderId == _selectedFolderFilter!.FolderId,
        _ => true,
    };

    [RelayCommand]
    private void SelectFolderFilter(FolderFilterOptionViewModel option)
    {
        foreach (FolderFilterOptionViewModel o in FolderFilterOptions)
        {
            o.IsSelected = ReferenceEquals(o, option);
        }

        _selectedFolderFilter = option;
        ApplyFilters();
    }

    /// <summary>The only place <see cref="IVaultItemRepository.SetFavorite"/> (Phase 4) is
    /// actually called from -- Phase 4 added the flag and the repository method but no UI ever
    /// set it, so <see cref="FavoritesOnly"/> would have had nothing to ever show. A full
    /// <see cref="Load"/> afterward, not an in-place mutation of the immutable
    /// <see cref="VaultItemListEntry"/> record, matching how every other mutation on this page
    /// (create/edit/delete via a form) already refreshes by reloading rather than patching one
    /// entry in place.</summary>
    [RelayCommand]
    private void ToggleFavorite(VaultItemListEntry entry)
    {
        try
        {
            _repository.SetFavorite(entry.Id, !entry.IsFavorite);
        }
        catch (Exception ex) when (ex is InvalidOperationException or VaultDatabaseException)
        {
            return;
        }

        Load();
    }

    [RelayCommand]
    private void ToggleTagFilter(TagChipViewModel chip)
    {
        chip.IsSelected = !chip.IsSelected;
        ApplyFilters();
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
    private static async Task ManageFoldersAsync() => await Shell.Current.GoToAsync("folder-management");

    [RelayCommand]
    private static async Task OpenPasswordGeneratorAsync() => await Shell.Current.GoToAsync("password-generator");

    [RelayCommand]
    private static async Task OpenBackupAsync() => await Shell.Current.GoToAsync("backup");

    [RelayCommand]
    private static async Task OpenSettingsAsync() => await Shell.Current.GoToAsync("settings");

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
