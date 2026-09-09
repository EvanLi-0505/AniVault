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

    [ObservableProperty]
    private ViewModelBase? _currentViewModel;

    [ObservableProperty]
    private NavItem? _selectedNavItem;

    public ShellViewModel(INavigationService navigation)
    {
        _navigation = navigation;
        _navigation.CurrentViewModelChanged += () => CurrentViewModel = _navigation.CurrentViewModel;

        NavItems = BuildNavItems();
    }

    public ObservableCollection<NavItem> NavItems { get; }

    /// <summary>Navigates to the first real page. Called by the shell once the window is shown.</summary>
    public void NavigateToStart()
    {
        var home = NavItems.First(i => i.Label == "Home");
        SelectedNavItem = home;
    }

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

    private static ObservableCollection<NavItem> BuildNavItems()
    {
        // Pages marked "later phase" route to a shared informational placeholder page so the
        // navigation skeleton is complete and nothing dead-ends. They are wired up in their phase.
        var items = new List<NavItem>
        {
            NavItem.Page("Home", "\U0001F3E0", nav => nav.NavigateTo<HomeViewModel>()),

            NavItem.Header("Libraries"),
            NavItem.Page("Anime", "\U0001F4FA", nav => nav.NavigateTo<LibraryViewModel>(vm => vm.SetMediaType(MediaType.Anime))),
            NavItem.Page("Movies", "\U0001F3AC", nav => nav.NavigateTo<LibraryViewModel>(vm => vm.SetMediaType(MediaType.Movie))),
            NavItem.Page("TV Series", "\U0001F4FA", nav => nav.NavigateTo<LibraryViewModel>(vm => vm.SetMediaType(MediaType.TvSeries))),

            NavItem.Header("Status"),
            StatusPage("Planned", "\U0001F4E5", WatchStatus.Planned),
            StatusPage("Watching", "▶", WatchStatus.Watching),
            StatusPage("Completed", "✓", WatchStatus.Completed),
            StatusPage("On Hold", "⏸", WatchStatus.OnHold),
            StatusPage("Dropped", "✕", WatchStatus.Dropped),

            NavItem.Header("Personal"),
            NavItem.Page("Favorites", "❤", nav => nav.NavigateTo<LibraryViewModel>(vm => vm.Configure(
                new LibraryPreset("Favorites", "Everything you marked as a favorite.", FavoriteOnly: true)))),
            NavItem.Page("Liked", "\U0001F44D", nav => nav.NavigateTo<LibraryViewModel>(vm => vm.Configure(
                new LibraryPreset("Liked", "Everything you marked as liked.", LikedOnly: true)))),
            NavItem.Page("My Rating", "⭐", nav => nav.NavigateTo<LibraryViewModel>(vm => vm.Configure(
                new LibraryPreset("By my rating", "Your library ordered by personal rating.",
                    SortField: MediaSortField.MyRating, SortDescending: true)))),
            NavItem.Page("Tags", "\U0001F3F7", nav => nav.NavigateTo<TagsViewModel>()),

            NavItem.Header("Browse"),
            NavItem.Page("Seasons", "\U0001F4C5", nav => nav.NavigateTo<SeasonsViewModel>()),
            NavItem.Page("Search", "\U0001F50D", nav => nav.NavigateTo<LibraryViewModel>(vm => vm.Configure(
                new LibraryPreset("Search", "Search and filter across every library.")))),

            NavItem.Header(string.Empty),
            NavItem.Page("Settings", "⚙", nav => nav.NavigateTo<SettingsViewModel>()),
        };

        return new ObservableCollection<NavItem>(items);
    }

    private static NavItem StatusPage(string label, string icon, WatchStatus status)
        => NavItem.Page(label, icon, nav => nav.NavigateTo<LibraryViewModel>(vm => vm.Configure(
            new LibraryPreset(label, $"Everything with the \"{label}\" status.", Status: status))));

    private static NavItem ComingSoon(string label, string icon, string feature)
        => NavItem.Page(label, icon, nav => nav.NavigateTo<PlaceholderViewModel>(vm => vm.Describe(label, feature)));
}
