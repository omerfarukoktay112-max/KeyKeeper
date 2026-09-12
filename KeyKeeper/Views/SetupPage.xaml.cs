using KeyKeeper.ViewModels;

namespace KeyKeeper.Views;

public partial class SetupPage : ContentPage
{
    private readonly SetupViewModel _viewModel;

    public SetupPage(SetupViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
        viewModel.Completed += OnSetupCompleted;
    }

    private void OnSetupCompleted() =>
        MainThread.BeginInvokeOnMainThread(async () => await Shell.Current.GoToAsync("//vault"));
}
