using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeyKeeper.Core.Data;
using KeyKeeper.Core.Models;

namespace KeyKeeper.ViewModels;

public sealed partial class LoginFormViewModel : VaultItemFormViewModelBase<LoginItem>
{
    public LoginFormViewModel(IVaultItemRepository repository) : base(repository)
    {
    }

    [ObservableProperty]
    private string title = string.Empty;

    [ObservableProperty]
    private string? username;

    [ObservableProperty]
    private string? password;

    [ObservableProperty]
    private string? url;

    [ObservableProperty]
    private string? totpSecret;

    /// <summary>Bound to the password Entry's <c>IsPassword</c> — masked by default, same as the
    /// master-password fields elsewhere in this app, so this isn't a second masking mechanism.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPasswordMasked))]
    private bool isPasswordVisible;

    public bool IsPasswordMasked => !IsPasswordVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTotpSecretMasked))]
    private bool isTotpSecretVisible;

    public bool IsTotpSecretMasked => !IsTotpSecretVisible;

    [RelayCommand]
    private void TogglePasswordVisibility() => IsPasswordVisible = !IsPasswordVisible;

    [RelayCommand]
    private void ToggleTotpSecretVisibility() => IsTotpSecretVisible = !IsTotpSecretVisible;

    protected override LoginItem BuildPayload() => new(Title, Username, Password, Url, TotpSecret);

    protected override void LoadFromPayload(LoginItem payload)
    {
        Title = payload.Title;
        Username = payload.Username;
        Password = payload.Password;
        Url = payload.Url;
        TotpSecret = payload.TotpSecret;
    }
}
