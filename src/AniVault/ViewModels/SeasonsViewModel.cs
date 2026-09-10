using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using AniVault.Models;
using AniVault.Services;
using AniVault.Utilities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AniVault.ViewModels;

/// <summary>One year in the season browser's year list.</summary>
public sealed partial class SeasonYearViewModel : ObservableObject
{
    [ObservableProperty] private bool _isSelected;

    public SeasonYearViewModel(int year, int totalCount)
    {
        Year = year;
        TotalCount = totalCount;
    }

    public int Year { get; }

    public int TotalCount { get; }
}

/// <summary>One of the four season tabs, with the count for the selected year.</summary>
public sealed partial class SeasonTabViewModel : ObservableObject
{
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private int _count;

    public SeasonTabViewModel(AnimeSeason season) => Season = season;

    public AnimeSeason Season { get; }

    public string Label => EnumDisplay.Label(Season);
}

/// <summary>
/// Browse anime by original broadcast year and season (e.g. "2026 Summer").
/// This is the broadcast season, not when the user watched it. Fully offline.
/// </summary>
public sealed partial class SeasonsViewModel : ViewModelBase
{
    private readonly IMediaQueryService _queryService;
    private readonly IMediaCardFactory _cards;
    private readonly INavigationService _navigation;
    private readonly ILogger<SeasonsViewModel> _logger;
    private readonly ILocalizationService _loc;

    private IReadOnlyList<AnimeSeasonBucket> _buckets = Array.Empty<AnimeSeasonBucket>();

    [ObservableProperty] private bool _hasNoAnime;
    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private string _currentSelectionLabel = string.Empty;

    public SeasonsViewModel(
        IMediaQueryService queryService,
        IMediaCardFactory cards,
        INavigationService navigation,
        ILocalizationService loc,
        ILogger<SeasonsViewModel> logger)
    {
        _queryService = queryService;
        _cards = cards;
        _navigation = navigation;
        _loc = loc;
        _logger = logger;
    }

    public ObservableCollection<SeasonYearViewModel> Years { get; } = new();

    public SeasonTabViewModel[] Seasons { get; } =
        Enum.GetValues<AnimeSeason>().Select(s => new SeasonTabViewModel(s)).ToArray();

    public ObservableCollection<MediaCardViewModel> Items { get; } = new();

    private int? SelectedYear => Years.FirstOrDefault(y => y.IsSelected)?.Year;

    private AnimeSeason? SelectedSeason => Seasons.FirstOrDefault(s => s.IsSelected)?.Season;

    public override async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            _buckets = await _queryService.GetAnimeSeasonBucketsAsync();

            Years.Clear();
            foreach (var group in _buckets.GroupBy(b => b.Year).OrderByDescending(g => g.Key))
            {
                Years.Add(new SeasonYearViewModel(group.Key, group.Sum(b => b.Count)));
            }

            HasNoAnime = Years.Count == 0;
            if (HasNoAnime)
            {
                Items.Clear();
                IsEmpty = true;
                return;
            }

            var (currentYear, currentSeason) = SeasonHelper.Current();
            var startYear = Years.Any(y => y.Year == currentYear) ? currentYear : Years[0].Year;
            SelectYearInternal(startYear, preferredSeason: currentSeason);

            await ReloadItemsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load the season browser.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SelectYear(SeasonYearViewModel? year)
    {
        if (year is null || year.IsSelected)
        {
            return;
        }

        SelectYearInternal(year.Year, preferredSeason: SelectedSeason);
        await ReloadItemsAsync();
    }

    [RelayCommand]
    private async Task SelectSeason(SeasonTabViewModel? season)
    {
        if (season is null || season.IsSelected)
        {
            return;
        }

        foreach (var tab in Seasons)
        {
            tab.IsSelected = ReferenceEquals(tab, season);
        }

        await ReloadItemsAsync();
    }

    [RelayCommand]
    private void OpenMedia(MediaCardViewModel? card)
    {
        if (card is not null)
        {
            _navigation.NavigateToDetail<MediaDetailViewModel>(vm => vm.MediaId = card.Id);
        }
    }

    private void SelectYearInternal(int year, AnimeSeason? preferredSeason)
    {
        foreach (var y in Years)
        {
            y.IsSelected = y.Year == year;
        }

        foreach (var tab in Seasons)
        {
            tab.Count = _buckets
                .Where(b => b.Year == year && b.Season == tab.Season)
                .Sum(b => b.Count);
        }

        // Keep the preferred season if it has content this year; otherwise pick the first that does.
        var target = preferredSeason is { } p && Seasons.First(s => s.Season == p).Count > 0
            ? p
            : Seasons.FirstOrDefault(s => s.Count > 0)?.Season ?? AnimeSeason.Winter;

        foreach (var tab in Seasons)
        {
            tab.IsSelected = tab.Season == target;
        }
    }

    private async Task ReloadItemsAsync()
    {
        if (SelectedYear is not { } year || SelectedSeason is not { } season)
        {
            return;
        }

        CurrentSelectionLabel = _loc.Format("Seasons.SelectionFormat", year, EnumDisplay.Label(season));

        var results = await _queryService.QueryAsync(
            new MediaFilter { MediaType = MediaType.Anime, Year = year, Season = season },
            new MediaSortOption(MediaSortField.MyRating, Descending: true));

        Items.Clear();
        foreach (var media in results)
        {
            Items.Add(_cards.Create(media));
        }

        IsEmpty = Items.Count == 0;
    }
}
