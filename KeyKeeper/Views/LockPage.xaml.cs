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
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = _viewModel.InitializeAsync();
    }

    private void OnUnlocked() =>
        MainThread.BeginInvokeOnMainThread(async () => await Shell.Current.GoToAsync("//vault"));
}
