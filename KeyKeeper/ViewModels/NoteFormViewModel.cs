using CommunityToolkit.Mvvm.ComponentModel;
using KeyKeeper.Core.Data;
using KeyKeeper.Core.Models;
using KeyKeeper.Core.Tools;

namespace KeyKeeper.ViewModels;

/// <summary>No sensitive-field masking here — see <see cref="SecureNoteItem"/>'s doc comment for
/// why: the whole note is the secret, there's no single field to single out.</summary>
public sealed partial class NoteFormViewModel : VaultItemFormViewModelBase<SecureNoteItem>
{
    public NoteFormViewModel(IVaultItemRepository repository, IFolderRepository folders, IClipboardClearingService clipboardClearingService)
        : base(repository, folders, clipboardClearingService)
    {
    }

    [ObservableProperty]
    private string title = string.Empty;

    [ObservableProperty]
    private string content = string.Empty;

    protected override SecureNoteItem BuildPayload() => new(Title, Content);

    protected override void LoadFromPayload(SecureNoteItem payload)
    {
        Title = payload.Title;
        Content = payload.Content;
    }
}
