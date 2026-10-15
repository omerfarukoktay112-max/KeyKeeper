using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeyKeeper.Core.Auth;
using KeyKeeper.Core.Crypto;
using KeyKeeper.Core.Data;
using KeyKeeper.Localization;

namespace KeyKeeper.ViewModels;

/// <summary>
/// Drives first-run vault setup: a master password entered twice, submitted via
/// <see cref="IAuthenticationService.InitializeVaultAsync"/>. Password-strength feedback is a
/// later ("Araçlar") phase — this only enforces <see cref="MinimumPasswordLength"/>.
/// </summary>
public sealed partial class SetupViewModel : ObservableObject
{
    /// <summary>
    /// A documented placeholder, same spirit as <see cref="Argon2Constants"/>'s KDF defaults —
    /// not a real strength requirement (that's the later password-strength-meter phase), just a
    /// floor against trivially short input.
    /// </summary>
    public const int MinimumPasswordLength = 8;

    private readonly IAuthenticationService _auth;

    public SetupViewModel(IAuthenticationService auth)
    {
        _auth = auth;
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
    private string password = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
    private string confirmPassword = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? errorMessage;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
    [NotifyPropertyChangedFor(nameof(AreInputsEnabled))]
    private bool isBusy;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool AreInputsEnabled => !IsBusy;

    /// <summary>Raised on success so the page can navigate away — kept out of this ViewModel to
    /// avoid giving it a Shell/navigation dependency.</summary>
    public event Action? Completed;

    private bool CanSubmit() =>
        !IsBusy && Password.Length > 0 && ConfirmPassword.Length > 0;

    [RelayCommand(CanExecute = nameof(CanSubmit))]
    private async Task SubmitAsync()
    {
        ErrorMessage = null;

        if (Password.Length < MinimumPasswordLength)
        {
            ErrorMessage = S.F(S.PasswordTooShort, MinimumPasswordLength);
            return;
        }

        if (Password != ConfirmPassword)
        {
            ErrorMessage = S.PasswordsMismatch;
            return;
        }

        IsBusy = true;
        char[] passwordChars = Password.ToCharArray();
        try
        {
            await _auth.InitializeVaultAsync(passwordChars);
            Completed?.Invoke();
        }
        catch (VaultDatabaseException)
        {
            ErrorMessage = S.VaultCreateFailed;
        }
        catch (InvalidOperationException)
        {
            // The vault was already initialized (e.g. a second concurrent tap) -- surface it
            // rather than letting the page pretend setup didn't happen.
            ErrorMessage = S.VaultAlreadySetup;
        }
        finally
        {
            SecureMemoryHelper.Clear(passwordChars);
            // Password/ConfirmPassword themselves stay as .NET strings here -- Entry.Text is
            // string-based, so they can't be reliably wiped the way the char[] above can (see
            // KeyKeeper.Core.Storage.WrappedKeyStorage's doc comment for the same acknowledged
            // string-boundary tradeoff). Clearing the bound properties at least drops this
            // ViewModel's own reference once the page is done with it.
            Password = string.Empty;
            ConfirmPassword = string.Empty;
            IsBusy = false;
        }
    }
}
