using System.ComponentModel;
using KeyKeeper.Core.Autofill;
using KeyKeeper.ViewModels;

namespace KeyKeeper.Views;

public partial class LockPage : ContentPage
{
    private readonly LockViewModel _viewModel;
    private readonly IAutofillCompletionHandler _autofillCompletion;

    public LockPage(LockViewModel viewModel, IAutofillCompletionHandler autofillCompletion)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _autofillCompletion = autofillCompletion;
        BindingContext = viewModel;
        viewModel.Unlocked += OnUnlocked;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = _viewModel.InitializeAsync();
    }

    /// <summary>Phase 10: when this unlock was triggered by tapping an Android autofill
    /// "authentication required" suggestion in another app, <see cref="IAutofillCompletionHandler.
    /// HasPendingRequest"/> is true -- <see cref="IAutofillCompletionHandler.Complete"/> re-derives
    /// the real autofill response from the now-unlocked vault and ends the activity itself, instead
    /// of navigating to <c>"//vault"</c> (there is no vault screen to show; the user never left
    /// their other app). A permanent no-op path on iOS -- <c>HasPendingRequest</c> is always false
    /// there, so this always falls through to the ordinary navigation. See
    /// <see cref="IAutofillCompletionHandler"/>'s own doc comment for the full flow.</summary>
    private void OnUnlocked()
    {
        if (_autofillCompletion.HasPendingRequest)
        {
            _autofillCompletion.Complete();
            return;
        }

        MainThread.BeginInvokeOnMainThread(async () => await Shell.Current.GoToAsync("//vault"));
    }

    /// <summary>Phase 9 micro-interaction: a small shake on the password field the moment a wrong
    /// password/lockout error appears, on top of (not instead of) the <c>ErrorLabel</c> text --
    /// purely decorative, so it's triggered from here (view-layer feedback) rather than added as
    /// another responsibility to <see cref="LockViewModel"/> itself.</summary>
    private async void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LockViewModel.HasError) && _viewModel.HasError)
        {
            await ShakeAsync(PasswordEntry);
        }
    }

    private static async Task ShakeAsync(VisualElement element)
    {
        const uint step = 60;
        await element.TranslateToAsync(-10, 0, step, Easing.Linear);
        await element.TranslateToAsync(10, 0, step, Easing.Linear);
        await element.TranslateToAsync(-6, 0, step, Easing.Linear);
        await element.TranslateToAsync(0, 0, step, Easing.Linear);
    }
}
