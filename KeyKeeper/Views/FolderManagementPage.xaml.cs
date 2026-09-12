using KeyKeeper.ViewModels;

namespace KeyKeeper.Views;

public partial class FolderManagementPage : ContentPage
{
    private readonly FolderManagementViewModel _viewModel;

    public FolderManagementPage(FolderManagementViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.Load();
    }
}
