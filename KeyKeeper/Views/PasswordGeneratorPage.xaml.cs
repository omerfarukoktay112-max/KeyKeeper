using KeyKeeper.ViewModels;

namespace KeyKeeper.Views;

public partial class PasswordGeneratorPage : ContentPage
{
    private readonly PasswordGeneratorViewModel _viewModel;

    public PasswordGeneratorPage(PasswordGeneratorViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.StopClipboardCountdown();
    }
}
