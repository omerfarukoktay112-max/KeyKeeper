using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeyKeeper.Core.Data;
using KeyKeeper.Core.Models;

namespace KeyKeeper.ViewModels;

public sealed partial class CardFormViewModel : VaultItemFormViewModelBase<CardItem>
{
    /// <summary>Display order for the <c>CardType</c> <c>Picker</c> in <c>CardFormPage.xaml</c> —
    /// index N here must stay <see cref="CardType"/>'s underlying value N, since
    /// <see cref="CardTypeIndex"/> casts directly between them instead of doing a lookup.</summary>
    public static readonly string[] CardTypeOptions = ["Visa", "Mastercard", "American Express", "Discover", "Diğer"];

    public CardFormViewModel(IVaultItemRepository repository, IFolderRepository folders) : base(repository, folders)
    {
    }

    [ObservableProperty]
    private string title = string.Empty;

    [ObservableProperty]
    private string? cardholderName;

    [ObservableProperty]
    private string? cardNumber;

    [ObservableProperty]
    private string? expiryMonthText;

    [ObservableProperty]
    private string? expiryYearText;

    [ObservableProperty]
    private string? cvv;

    [ObservableProperty]
    private int cardTypeIndex;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCardNumberMasked))]
    private bool isCardNumberVisible;

    public bool IsCardNumberMasked => !IsCardNumberVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCvvMasked))]
    private bool isCvvVisible;

    public bool IsCvvMasked => !IsCvvVisible;

    [RelayCommand]
    private void ToggleCardNumberVisibility() => IsCardNumberVisible = !IsCardNumberVisible;

    [RelayCommand]
    private void ToggleCvvVisibility() => IsCvvVisible = !IsCvvVisible;

    protected override CardItem BuildPayload() => new(
        Title,
        CardholderName,
        CardNumber,
        int.TryParse(ExpiryMonthText, out int month) ? month : null,
        int.TryParse(ExpiryYearText, out int year) ? year : null,
        Cvv,
        (CardType)CardTypeIndex);

    protected override void LoadFromPayload(CardItem payload)
    {
        Title = payload.Title;
        CardholderName = payload.CardholderName;
        CardNumber = payload.CardNumber;
        ExpiryMonthText = payload.ExpiryMonth?.ToString();
        ExpiryYearText = payload.ExpiryYear?.ToString();
        Cvv = payload.Cvv;
        CardTypeIndex = (int)payload.CardType;
    }
}
