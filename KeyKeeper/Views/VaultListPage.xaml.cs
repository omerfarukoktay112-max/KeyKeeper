using KeyKeeper.ViewModels;

namespace KeyKeeper.Views;

public partial class VaultListPage : ContentPage
{
    private readonly VaultListViewModel _viewModel;

    public VaultListPage(VaultListViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Every time the page becomes visible again, not just the first time -- this is how the
        // list picks up a create/edit/delete made on a form page just navigated back from.
        _ = _viewModel.InitializeAsync();
    }
}
