using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeyKeeper.Core.Crypto;
using KeyKeeper.Core.Data;
using KeyKeeper.Core.Models;
using KeyKeeper.Core.Tools;
using KeyKeeper.Services;
using Microsoft.Maui.Dispatching;
using KeyKeeper.Localization;

namespace KeyKeeper.ViewModels;

/// <summary>One entry in a form's folder <c>Picker</c> -- <see cref="Id"/> of <c>null</c> is the
/// always-present "Klasörsüz" (unfiled) option; every other entry is a real folder, indented per
/// <see cref="FolderTreeFlattener"/> to show nesting in a flat Picker list.</summary>
public sealed record FolderPickerOption(string? Id, string DisplayName);

/// <summary>
/// Shared add/edit/view logic for all five vault item forms (<see cref="LoginFormViewModel"/>,
/// <see cref="CardFormViewModel"/>, <see cref="WifiFormViewModel"/>, <see cref="NoteFormViewModel"/>,
/// <see cref="LicenseFormViewModel"/>) — one page per type doubles as both its add form and its
/// edit/detail view, distinguished by whether <see cref="ItemId"/> is set: create mode (no id,
/// reached from <c>VaultListPage</c>'s "add" menu) vs. edit mode (an existing item's id, reached
/// by tapping it in the list). There's no separate read-only "detail" page — showing an existing
/// item's fields *is* its edit form, just initially masked for sensitive fields (each subclass's
/// own concern, via a per-field reveal toggle) rather than a whole second page.
///
/// Generic over <typeparamref name="TPayload"/> (not just <see cref="IVaultItemPayload"/>) so
/// <see cref="Save"/> can call <see cref="IVaultItemRepository.Create{TPayload}"/>/
/// <see cref="IVaultItemRepository.Update{TPayload}"/> directly, with the compiler (not a runtime
/// switch) guaranteeing the payload this page builds matches the item type it's the form for.
///
/// Folder/tag assignment (Phase 5) lives here, not in the typed payload -- <see cref="IVaultItemRepository.SetFolder"/>/
/// <see cref="IVaultItemRepository.AddTag"/>/<see cref="IVaultItemRepository.RemoveTag"/> are
/// separate calls from <see cref="IVaultItemRepository.Create{TPayload}"/>'s/<see cref="IVaultItemRepository.Update{TPayload}"/>'s payload
/// write, so <see cref="Save"/> makes them as extra steps: <see cref="SelectedFolderOption"/>'s id
/// is passed straight into <c>Create</c> for a new item, or applied via <c>SetFolder</c>
/// unconditionally after <c>Update</c> for an existing one (idempotent either way, simpler than
/// tracking whether it actually changed); <see cref="Tags"/> is diffed against the snapshot
/// <see cref="Initialize"/> loaded, so only the tags actually added/removed since then trigger a
/// call, not every tag on every save.
/// </summary>
public abstract partial class VaultItemFormViewModelBase<TPayload> : ObservableObject
    where TPayload : IVaultItemPayload
{
    /// <summary>How long a value copied via <see cref="CopyToClipboardAsync"/> stays on the
    /// clipboard before <see cref="IClipboardClearingService"/> wipes it -- picked from the
    /// middle of the 30-45s range the "Araçlar" phase calls for.</summary>
    private static readonly TimeSpan ClipboardClearDelay = TimeSpan.FromSeconds(30);

    protected readonly IVaultItemRepository Repository;
    private readonly IFolderRepository _folders;
    private readonly IClipboardClearingService _clipboardClearingService;
    private HashSet<string> _originalTags = [];

    private IDispatcherTimer? _clipboardCountdownTimer;
    private DateTimeOffset _clipboardClearAtUtc;

    protected VaultItemFormViewModelBase(IVaultItemRepository repository, IFolderRepository folders, IClipboardClearingService clipboardClearingService)
    {
        Repository = repository;
        _folders = folders;
        _clipboardClearingService = clipboardClearingService;
    }

    /// <summary>Set by the page from Shell navigation query parameters before <see cref="Initialize"/>
    /// is called — present (edit mode) or <c>null</c> (create mode).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditMode))]
    private string? itemId;

    public bool IsEditMode => ItemId is not null;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? errorMessage;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public ObservableCollection<FolderPickerOption> FolderOptions { get; } = [];

    [ObservableProperty]
    private FolderPickerOption? selectedFolderOption;

    public ObservableCollection<string> Tags { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAddTag))]
    [NotifyCanExecuteChangedFor(nameof(AddTagCommand))]
    private string? newTagText;

    public bool CanAddTag => !string.IsNullOrWhiteSpace(NewTagText) && !Tags.Contains(NewTagText, StringComparer.Ordinal);

    /// <summary>Feedback shown next to a form's "Kopyala" buttons, e.g. "Panoya kopyalandı, 30
    /// saniye sonra temizlenecek." — null when nothing was copied recently.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasClipboardStatus))]
    private string? clipboardStatus;

    public bool HasClipboardStatus => !string.IsNullOrEmpty(ClipboardStatus);

    /// <summary>Copies a sensitive field's current value to the clipboard, scheduling it to
    /// auto-clear via <see cref="IClipboardClearingService"/> — bind a form's "Kopyala" buttons
    /// to this with the field (e.g. <c>Password</c>) as <c>CommandParameter</c>.</summary>
    [RelayCommand]
    private async Task CopyToClipboardAsync(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        await _clipboardClearingService.CopyAsync(value, ClipboardClearDelay);
        StartClipboardCountdown();
    }

    private void StartClipboardCountdown()
    {
        _clipboardClearAtUtc = DateTimeOffset.UtcNow + ClipboardClearDelay;
        UpdateClipboardStatus();

        _clipboardCountdownTimer ??= CreateClipboardCountdownTimer();
        _clipboardCountdownTimer.Start();
    }

    private IDispatcherTimer CreateClipboardCountdownTimer()
    {
        IDispatcherTimer timer = Application.Current!.Dispatcher.CreateTimer();
        timer.Interval = TimeSpan.FromSeconds(1);
        timer.IsRepeating = true;
        timer.Tick += (_, _) => OnClipboardCountdownTick();
        return timer;
    }

    private void OnClipboardCountdownTick()
    {
        if (DateTimeOffset.UtcNow >= _clipboardClearAtUtc)
        {
            _clipboardCountdownTimer?.Stop();
            ClipboardStatus = null;
            return;
        }

        UpdateClipboardStatus();
    }

    private void UpdateClipboardStatus()
    {
        TimeSpan remaining = _clipboardClearAtUtc - DateTimeOffset.UtcNow;
        if (remaining < TimeSpan.Zero)
        {
            remaining = TimeSpan.Zero;
        }

        ClipboardStatus = S.F(S.ClipboardCopied, (int)Math.Ceiling(remaining.TotalSeconds));
    }

    /// <summary>Call from the page's <c>OnDisappearing</c> so the 1-second countdown timer
    /// doesn't keep ticking after the user has navigated away.</summary>
    public void StopClipboardCountdown() => _clipboardCountdownTimer?.Stop();

    /// <summary>Raised after a successful create/update so the page can navigate back.</summary>
    public event Action? Saved;

    /// <summary>Raised after a successful delete so the page can navigate back.</summary>
    public event Action? Deleted;

    /// <summary>Builds the payload to save from the page's bound fields.</summary>
    protected abstract TPayload BuildPayload();

    /// <summary>Populates the page's bound fields from a decrypted existing item (edit mode).</summary>
    protected abstract void LoadFromPayload(TPayload payload);

    /// <summary>Call once <see cref="ItemId"/> is known (from the page's <c>ApplyQueryAttributes</c>)
    /// — loads folder options in both modes; loads and decrypts the existing item, its folder, and
    /// its tags in edit mode; a no-op beyond folder options in create mode.
    ///
    /// Guarded against a locked vault the same way as <c>FolderManagementViewModel</c>'s commands
    /// (see its <c>TryRun</c> doc comment) -- the inactivity timer can fire between this page
    /// being navigated to and this method actually running, and a repository call against a
    /// locked vault throws rather than returning empty. <c>App.xaml.cs</c>'s central
    /// <c>Locked</c> subscriber is already navigating away in that case, so this just needs to
    /// not crash in the meantime, not show a useful error.</summary>
    public void Initialize()
    {
        try
        {
            LoadFolderOptions();

            if (ItemId is null)
            {
                return;
            }

            VaultItemRecord? record = Repository.Get(ItemId);
            if (record?.Payload is TPayload payload)
            {
                LoadFromPayload(payload);
                SelectedFolderOption = FolderOptions.FirstOrDefault(o => o.Id == record.FolderId) ?? FolderOptions[0];
                _originalTags = record.Tags.ToHashSet(StringComparer.Ordinal);
                Tags.Clear();
                foreach (string tag in record.Tags)
                {
                    Tags.Add(tag);
                }
            }
            else
            {
                ErrorMessage = S.ItemNotFound;
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or VaultDatabaseException)
        {
            // Vault locked out from under this call; the central Locked subscriber is already
            // navigating to the lock screen.
        }
    }

    private void LoadFolderOptions()
    {
        FolderOptions.Clear();
        FolderOptions.Add(new FolderPickerOption(null, S.Unfiled));
        foreach (IndentedFolder folder in FolderTreeFlattener.Flatten(_folders.ListAll()))
        {
            FolderOptions.Add(new FolderPickerOption(folder.Id, folder.DisplayName));
        }

        SelectedFolderOption ??= FolderOptions[0];
    }

    [RelayCommand(CanExecute = nameof(CanAddTag))]
    private void AddTag()
    {
        Tags.Add(NewTagText!);
        NewTagText = null;
    }

    [RelayCommand]
    private void RemoveTag(string tag) => Tags.Remove(tag);

    [RelayCommand]
    private void Save()
    {
        ErrorMessage = null;

        TPayload payload = BuildPayload();
        if (string.IsNullOrWhiteSpace(payload.Title))
        {
            ErrorMessage = S.EnterTitle;
            return;
        }

        IsBusy = true;
        try
        {
            string id;
            if (ItemId is null)
            {
                id = Repository.Create(payload, SelectedFolderOption?.Id).Id;
            }
            else
            {
                id = ItemId;
                Repository.Update(id, payload);
                Repository.SetFolder(id, SelectedFolderOption?.Id);
            }

            ApplyTagChanges(id);
            Saved?.Invoke();
        }
        catch (Exception ex) when (ex is InvalidOperationException or VaultDatabaseException or VaultCryptographyException)
        {
            ErrorMessage = S.SaveFailed;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyTagChanges(string id)
    {
        var current = Tags.ToHashSet(StringComparer.Ordinal);
        foreach (string removed in _originalTags.Except(current))
        {
            Repository.RemoveTag(id, removed);
        }

        foreach (string added in current.Except(_originalTags))
        {
            Repository.AddTag(id, added);
        }
    }

    [RelayCommand]
    private void Delete()
    {
        if (ItemId is null)
        {
            return;
        }

        try
        {
            Repository.Delete(ItemId);
        }
        catch (Exception ex) when (ex is InvalidOperationException or VaultDatabaseException)
        {
            ErrorMessage = S.VaultLockedRetry;
            return;
        }

        Deleted?.Invoke();
    }
}
