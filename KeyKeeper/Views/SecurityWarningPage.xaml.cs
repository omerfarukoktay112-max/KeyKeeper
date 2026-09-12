using KeyKeeper.ViewModels;

namespace KeyKeeper.Views;

public partial class SecurityWarningPage : ContentPage
{
    private readonly SecurityWarningViewModel _viewModel;

    public SecurityWarningPage(SecurityWarningViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
        viewModel.Continued += OnContinued;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.Initialize();
    }

    private void OnContinued(string route) =>
        MainThread.BeginInvokeOnMainThread(async () => await Shell.Current.GoToAsync(route));
}
