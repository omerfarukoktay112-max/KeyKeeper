using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeyKeeper.Core.Data;
using KeyKeeper.Core.Models;

namespace KeyKeeper.ViewModels;

public sealed partial class WifiFormViewModel : VaultItemFormViewModelBase<WifiItem>
{
    /// <summary>Display order for the <c>SecurityProtocol</c> <c>Picker</c> in
    /// <c>WifiFormPage.xaml</c> — index N here must stay <see cref="WifiSecurityProtocol"/>'s
    /// underlying value N, since <see cref="SecurityProtocolIndex"/> casts directly between them.</summary>
    public static readonly string[] SecurityProtocolOptions = ["Açık (şifresiz)", "WEP", "WPA2", "WPA3"];

    public WifiFormViewModel(IVaultItemRepository repository) : base(repository)
    {
    }

    [ObservableProperty]
    private string title = string.Empty;

    [ObservableProperty]
    private string ssid = string.Empty;

    [ObservableProperty]
    private string? password;

    [ObservableProperty]
    private int securityProtocolIndex = (int)WifiSecurityProtocol.Wpa2;

    [ObservableProperty]
    private bool isHidden;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPasswordMasked))]
    private bool isPasswordVisible;

    public bool IsPasswordMasked => !IsPasswordVisible;

    [RelayCommand]
    private void TogglePasswordVisibility() => IsPasswordVisible = !IsPasswordVisible;

    protected override WifiItem BuildPayload() =>
        new(Title, Ssid, Password, (WifiSecurityProtocol)SecurityProtocolIndex, IsHidden);

    protected override void LoadFromPayload(WifiItem payload)
    {
        Title = payload.Title;
        Ssid = payload.Ssid;
        Password = payload.Password;
        SecurityProtocolIndex = (int)payload.SecurityProtocol;
        IsHidden = payload.IsHidden;
    }
}
