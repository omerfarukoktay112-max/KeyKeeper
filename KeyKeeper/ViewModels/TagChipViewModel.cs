using CommunityToolkit.Mvvm.ComponentModel;

namespace KeyKeeper.ViewModels;

/// <summary>One toggleable tag filter chip on <c>VaultListPage</c>. A plain
/// <see cref="ObservableObject"/> (not a record) because <see cref="IsSelected"/> needs to change
/// in place and notify the UI -- <c>VaultListViewModel.ToggleTagFilterCommand</c> flips it and
/// re-runs filtering.</summary>
public sealed partial class TagChipViewModel : ObservableObject
{
    public string Id { get; }

    public string Name { get; }

    [ObservableProperty]
    private bool isSelected;

    public TagChipViewModel(string id, string name)
    {
        Id = id;
        Name = name;
    }
}
