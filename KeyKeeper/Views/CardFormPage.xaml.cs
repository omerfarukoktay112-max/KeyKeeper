using KeyKeeper.ViewModels;
using KeyKeeper.Localization;

namespace KeyKeeper.Views;

public partial class CardFormPage : ContentPage, IQueryAttributable
{
    private readonly CardFormViewModel _viewModel;
    private bool _initialized;

    public CardFormPage(CardFormViewModel viewModel)
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
        bool confirmed = await this.DisplayAlertAsync(S.DeleteItemTitle, S.DeleteItemMessage, S.Delete, S.CancelAction);
        if (confirmed)
        {
            _viewModel.DeleteCommand.Execute(null);
        }
    }

    private void OnSavedOrDeleted() =>
        MainThread.BeginInvokeOnMainThread(async () => await Shell.Current.GoToAsync(".."));
}
