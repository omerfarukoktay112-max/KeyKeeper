using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeyKeeper.Core.Data;
using KeyKeeper.Services;
using KeyKeeper.Localization;

namespace KeyKeeper.ViewModels;

/// <summary>One row in <c>FolderManagementPage</c>'s list -- <see cref="Name"/> is the raw folder
/// name (used as the rename prompt's starting value), <see cref="DisplayName"/> is the same name
/// indented per <see cref="FolderTreeFlattener"/> for display.</summary>
public sealed record FolderListEntry(string Id, string Name, string DisplayName);

/// <summary>
/// Basic folder CRUD screen -- create (optionally nested under an existing folder), rename, and
/// delete. Deliberately simple: rename and delete confirmation use <c>Shell.Current.DisplayPromptAsync</c>/
/// <c>DisplayAlert</c> directly from here rather than a dedicated edit page or inline-editable
/// list rows, the same "ViewModel calls Shell.Current directly for a lightweight dialog" precedent
/// <c>VaultListViewModel.AddAsync</c> already set with <c>DisplayActionSheetAsync</c> — a full
/// tree-editing UI (drag-to-reparent, inline rename) is more UI investment than "basit bir
/// yönetim ekranı" (a simple management screen) called for.
/// </summary>
public sealed partial class FolderManagementViewModel : ObservableObject
{
    private readonly IFolderRepository _folders;

    public FolderManagementViewModel(IFolderRepository folders)
    {
        _folders = folders;
    }

    public ObservableCollection<FolderListEntry> Folders { get; } = [];

    public ObservableCollection<FolderPickerOption> ParentOptions { get; } = [];

    [ObservableProperty]
    private FolderPickerOption? selectedParentOption;

    [ObservableProperty]
    private string? newFolderName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? errorMessage;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    /// <summary>Call from the page's <c>OnAppearing</c> — reloads every time the page becomes
    /// visible, same reasoning as <c>VaultListViewModel.Load</c>. Wrapped in the same
    /// locked-vault guard as every command below: the vault can auto-lock (inactivity timer)
    /// while this page sits idle behind a native dialog (<see cref="RenameAsync"/>'s prompt,
    /// <see cref="DeleteAsync"/>'s confirmation) — those dialogs are OS UI, not part of this
    /// page's own visual tree, so <c>PageActivityBehavior</c> can't see them and the countdown
    /// keeps running underneath. Without this guard, dismissing such a dialog after the lock
    /// fired would crash the app on the next repository call instead of just failing quietly.
    /// </summary>
    public void Load() => TryRun(() =>
    {
        IReadOnlyList<FolderRecord> all = _folders.ListAll();
        IReadOnlyList<IndentedFolder> flattened = FolderTreeFlattener.Flatten(all);

        Folders.Clear();
        foreach (IndentedFolder folder in flattened)
        {
            Folders.Add(new FolderListEntry(folder.Id, folder.Name, folder.DisplayName));
        }

        string? previouslySelectedParentId = SelectedParentOption?.Id;
        ParentOptions.Clear();
        ParentOptions.Add(new FolderPickerOption(null, S.RootFolder));
        foreach (IndentedFolder folder in flattened)
        {
            ParentOptions.Add(new FolderPickerOption(folder.Id, folder.DisplayName));
        }

        SelectedParentOption = ParentOptions.FirstOrDefault(o => o.Id == previouslySelectedParentId) ?? ParentOptions[0];
    });

    [RelayCommand]
    private void CreateFolder()
    {
        ErrorMessage = null;
        if (string.IsNullOrWhiteSpace(NewFolderName))
        {
            ErrorMessage = S.EnterFolderName;
            return;
        }

        if (TryRun(() => _folders.Create(NewFolderName, SelectedParentOption?.Id)))
        {
            NewFolderName = null;
            Load();
        }
    }

    [RelayCommand]
    private async Task RenameAsync(FolderListEntry entry)
    {
        string? newName = await Shell.Current.DisplayPromptAsync(S.RenameFolderTitle, S.NewNamePrompt, initialValue: entry.Name);
        if (string.IsNullOrWhiteSpace(newName) || newName == entry.Name)
        {
            return;
        }

        if (TryRun(() => _folders.Rename(entry.Id, newName)))
        {
            Load();
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(FolderListEntry entry)
    {
        bool confirmed = await Shell.Current.DisplayAlertAsync(
            S.DeleteFolderTitle,
            S.F(S.DeleteFolderMessage, entry.Name),
            S.Delete,
            S.CancelAction);
        if (!confirmed)
        {
            return;
        }

        if (TryRun(() => _folders.Delete(entry.Id)))
        {
            Load();
        }
    }

    /// <summary>Runs a repository call, turning a locked-vault failure into
    /// <see cref="ErrorMessage"/> instead of an unhandled exception -- see <see cref="Load"/>'s
    /// doc comment for why this can happen on this page specifically. The central
    /// <c>IAuthenticationService.Locked</c> subscriber (<c>App.xaml.cs</c>) already navigates to
    /// the lock screen the moment the lock actually fires; this just keeps that race from
    /// crashing the app in the meantime.</summary>
    private bool TryRun(Action action)
    {
        try
        {
            action();
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or VaultDatabaseException)
        {
            ErrorMessage = S.VaultLockedRetry;
            return false;
        }
    }
}
