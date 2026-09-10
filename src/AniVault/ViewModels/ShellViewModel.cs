using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AniVault.Models;
using AniVault.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AniVault.ViewModels;

/// <summary>
/// The main window ViewModel: owns the left navigation sidebar and hosts the current page.
/// </summary>
public sealed partial class ShellViewModel : ObservableObject
{
    private readonly INavigationService _navigation;
    private readonly LocalizationService _loc;

    [ObservableProperty]
    private ViewModelBase? _currentViewModel;

    [ObservableProperty]
    private NavItem? _selectedNavItem;

    public ShellViewModel(INavigationService navigation, LocalizationService localization)
    {
        _navigation = navigation;
        _loc = localization;
        _navigation.CurrentViewModelChanged += () => CurrentViewModel = _navigation.CurrentViewModel;

        NavItems = new ObservableCollection<NavItem>(BuildNavItems());

        localization.LanguageChanged += OnLanguageChanged;
    }

    public ObservableCollection<NavItem> NavItems { get; }

    public string AppName => _loc.Text("App.Name");

    public string AppTagline => _loc.Text("App.Tagline");

    /// <summary>Navigates to the first real page. Called by the shell once the window is shown.</summary>
    public void NavigateToStart() => SelectedNavItem = NavItems.First(i => i.Key == "Home");

    partial void OnSelectedNavItemChanged(NavItem? value)
    {
        if (value is { IsSelectable: true, Navigate: { } navigate })
        {
            navigate(_navigation);
        }
    }

    [RelayCommand]
    private void Navigate(NavItem? item)
    {
        if (item is { IsSelectable: true })
        {
            SelectedNavItem = item;
        }
    }

    private void OnLanguageChanged()
    {
        var selectedKey = SelectedNavItem?.Key;

        NavItems.Clear();
        foreach (var item in BuildNavItems())
        {
            NavItems.Add(item);
        }

        OnPropertyChanged(nameof(AppName));
        OnPropertyChanged(nameof(AppTagline));

        // Re-select (and therefore re-navigate) so the current page rebuilds its strings too.
        var match = NavItems.FirstOrDefault(i => i.Key == selectedKey);
        if (match is not null)
        {
            SelectedNavItem = match;
        }
    }

    private List<NavItem> BuildNavItems()
    {
        string L(string key) => _loc.Text(key);

        return new List<NavItem>
        {
            NavItem.Page("Home", L("Nav.Home"), "\U0001F3E0", nav => nav.NavigateTo<HomeViewModel>()),

            NavItem.Header(L("Nav.Section.Libraries")),
            NavItem.Page("Anime", L("Nav.Anime"), "\U0001F4FA", nav => nav.NavigateTo<LibraryViewModel>(vm => vm.SetMediaType(MediaType.Anime))),
            NavItem.Page("Movies", L("Nav.Movies"), "\U0001F3AC", nav => nav.NavigateTo<LibraryViewModel>(vm => vm.SetMediaType(MediaType.Movie))),
            NavItem.Page("TvSeries", L("Nav.TvSeries"), "\U0001F4FA", nav => nav.NavigateTo<LibraryViewModel>(vm => vm.SetMediaType(MediaType.TvSeries))),

            NavItem.Header(L("Nav.Section.Status")),
            StatusPage("Planned", L("Status.Short.Planned"), "\U0001F4E5", WatchStatus.Planned),
            StatusPage("Watching", L("Status.Short.Watching"), "▶", WatchStatus.Watching),
            StatusPage("Completed", L("Status.Short.Completed"), "✓", WatchStatus.Completed),
            StatusPage("OnHold", L("Status.Short.OnHold"), "⏸", WatchStatus.OnHold),
            StatusPage("Dropped", L("Status.Short.Dropped"), "✕", WatchStatus.Dropped),

            NavItem.Header(L("Nav.Section.Personal")),
            NavItem.Page("Favorites", L("Nav.Favorites"), "❤", nav => nav.NavigateTo<LibraryViewModel>(vm => vm.Configure(
                new LibraryPreset(L("Library.Preset.Favorites"), L("Library.Preset.FavoritesSub"), FavoriteOnly: true)))),
            NavItem.Page("Liked", L("Nav.Liked"), "\U0001F44D", nav => nav.NavigateTo<LibraryViewModel>(vm => vm.Configure(
                new LibraryPreset(L("Library.Preset.Liked"), L("Library.Preset.LikedSub"), LikedOnly: true)))),
            NavItem.Page("MyRating", L("Nav.MyRating"), "⭐", nav => nav.NavigateTo<LibraryViewModel>(vm => vm.Configure(
                new LibraryPreset(L("Library.Preset.MyRating"), L("Library.Preset.MyRatingSub"),
                    SortField: MediaSortField.MyRating, SortDescending: true)))),
            NavItem.Page("Tags", L("Nav.Tags"), "\U0001F3F7", nav => nav.NavigateTo<TagsViewModel>()),

            NavItem.Header(L("Nav.Section.Browse")),
            NavItem.Page("Seasons", L("Nav.Seasons"), "\U0001F4C5", nav => nav.NavigateTo<SeasonsViewModel>()),
            NavItem.Page("Search", L("Nav.Search"), "\U0001F50D", nav => nav.NavigateTo<LibraryViewModel>(vm => vm.Configure(
                new LibraryPreset(L("Library.Preset.Search"), L("Library.Preset.SearchSub"))))),

            NavItem.Header(string.Empty),
            NavItem.Page("Settings", L("Nav.Settings"), "⚙", nav => nav.NavigateTo<SettingsViewModel>()),
        };
    }

    private NavItem StatusPage(string key, string label, string icon, WatchStatus status)
        => NavItem.Page(key, label, icon, nav => nav.NavigateTo<LibraryViewModel>(vm => vm.Configure(
            new LibraryPreset(label, _loc.Format("Library.Preset.StatusSubFormat", label), Status: status))));
}
