using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeyKeeper.Core.Crypto;
using KeyKeeper.Core.Data;
using KeyKeeper.Core.Models;

namespace KeyKeeper.ViewModels;

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
/// </summary>
public abstract partial class VaultItemFormViewModelBase<TPayload> : ObservableObject
    where TPayload : IVaultItemPayload
{
    protected readonly IVaultItemRepository Repository;

    protected VaultItemFormViewModelBase(IVaultItemRepository repository)
    {
        Repository = repository;
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

    /// <summary>Raised after a successful create/update so the page can navigate back.</summary>
    public event Action? Saved;

    /// <summary>Raised after a successful delete so the page can navigate back.</summary>
    public event Action? Deleted;

    /// <summary>Builds the payload to save from the page's bound fields.</summary>
    protected abstract TPayload BuildPayload();

    /// <summary>Populates the page's bound fields from a decrypted existing item (edit mode).</summary>
    protected abstract void LoadFromPayload(TPayload payload);

    /// <summary>Call once <see cref="ItemId"/> is known (from the page's <c>ApplyQueryAttributes</c>)
    /// — loads and decrypts the existing item in edit mode; a no-op in create mode.</summary>
    public void Initialize()
    {
        if (ItemId is null)
        {
            return;
        }

        VaultItemRecord? record = Repository.Get(ItemId);
        if (record?.Payload is TPayload payload)
        {
            LoadFromPayload(payload);
        }
        else
        {
            ErrorMessage = "Öğe bulunamadı.";
        }
    }

    [RelayCommand]
    private void Save()
    {
        ErrorMessage = null;

        TPayload payload = BuildPayload();
        if (string.IsNullOrWhiteSpace(payload.Title))
        {
            ErrorMessage = "Bir başlık girin.";
            return;
        }

        IsBusy = true;
        try
        {
            if (ItemId is null)
            {
                Repository.Create(payload);
            }
            else
            {
                Repository.Update(ItemId, payload);
            }

            Saved?.Invoke();
        }
        catch (Exception ex) when (ex is InvalidOperationException or VaultDatabaseException or VaultCryptographyException)
        {
            ErrorMessage = "Kaydedilemedi. Lütfen tekrar deneyin.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Delete()
    {
        if (ItemId is null)
        {
            return;
        }

        Repository.Delete(ItemId);
        Deleted?.Invoke();
    }
}
