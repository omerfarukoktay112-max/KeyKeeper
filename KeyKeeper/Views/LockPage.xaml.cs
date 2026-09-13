using System.ComponentModel;
using KeyKeeper.ViewModels;

namespace KeyKeeper.Views;

public partial class LockPage : ContentPage
{
    private readonly LockViewModel _viewModel;

    public LockPage(LockViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
        viewModel.Unlocked += OnUnlocked;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = _viewModel.InitializeAsync();
    }

    private void OnUnlocked() =>
        MainThread.BeginInvokeOnMainThread(async () => await Shell.Current.GoToAsync("//vault"));

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
