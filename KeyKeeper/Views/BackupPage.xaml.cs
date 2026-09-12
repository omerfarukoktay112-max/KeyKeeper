using KeyKeeper.ViewModels;

namespace KeyKeeper.Views;

public partial class BackupPage : ContentPage
{
    public BackupPage(BackupViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
