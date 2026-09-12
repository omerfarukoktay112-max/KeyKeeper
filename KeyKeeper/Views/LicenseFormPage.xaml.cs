using KeyKeeper.ViewModels;

namespace KeyKeeper.Views;

public partial class LicenseFormPage : ContentPage, IQueryAttributable
{
    private readonly LicenseFormViewModel _viewModel;
    private bool _initialized;

    public LicenseFormPage(LicenseFormViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
        viewModel.Saved += OnSavedOrDeleted;
        viewModel.Deleted += OnSavedOrDeleted;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query) =>
        _viewModel.ItemId = query.TryGetValue("id", out object? value) ? value as string : null;

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (!_initialized)
        {
            _initialized = true;
            _viewModel.Initialize();
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.StopClipboardCountdown();
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        bool confirmed = await this.DisplayAlertAsync("Silinsin mi?", "Bu öğe kalıcı olarak silinecek.", "Sil", "Vazgeç");
        if (confirmed)
        {
            _viewModel.DeleteCommand.Execute(null);
        }
    }

    private void OnSavedOrDeleted() =>
        MainThread.BeginInvokeOnMainThread(async () => await Shell.Current.GoToAsync(".."));
}
