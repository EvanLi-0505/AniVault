using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using AniVault.Models;
using AniVault.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AniVault.ViewModels;

/// <summary>
/// Lightweight overview page. Every section comes from the local database only —
/// it never triggers a network request.
/// </summary>
public sealed partial class HomeViewModel : ViewModelBase
{
    private const int RowSize = 10;

    private readonly IMediaService _mediaService;
    private readonly IMediaQueryService _queryService;
    private readonly IMediaCardFactory _cards;
    private readonly INavigationService _navigation;
    private readonly ILogger<HomeViewModel> _logger;

    [ObservableProperty] private int _animeCount;
    [ObservableProperty] private int _movieCount;
    [ObservableProperty] private int _tvCount;
    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private bool _hasContinueWatching;
    [ObservableProperty] private bool _hasHighestRated;

    public HomeViewModel(
        IMediaService mediaService,
        IMediaQueryService queryService,
        IMediaCardFactory cards,
        INavigationService navigation,
        ILogger<HomeViewModel> logger)
    {
        _mediaService = mediaService;
        _queryService = queryService;
        _cards = cards;
        _navigation = navigation;
        _logger = logger;
    }

    public ObservableCollection<MediaCardViewModel> RecentlyAdded { get; } = new();

    public ObservableCollection<MediaCardViewModel> ContinueWatching { get; } = new();

    public ObservableCollection<MediaCardViewModel> HighestRated { get; } = new();

    public int TotalCount => AnimeCount + MovieCount + TvCount;

    public override async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            AnimeCount = await _mediaService.CountAsync(MediaType.Anime);
            MovieCount = await _mediaService.CountAsync(MediaType.Movie);
            TvCount = await _mediaService.CountAsync(MediaType.TvSeries);
            OnPropertyChanged(nameof(TotalCount));
            IsEmpty = TotalCount == 0;

            Fill(RecentlyAdded, await _mediaService.GetRecentlyAddedAsync(RowSize));

            var watching = await _queryService.QueryAsync(
                new MediaFilter { Status = WatchStatus.Watching },
                new MediaSortOption(MediaSortField.UpdatedDate, Descending: true));
            Fill(ContinueWatching, watching.Take(RowSize));
            HasContinueWatching = ContinueWatching.Count > 0;

            var rated = await _queryService.QueryAsync(
                new MediaFilter { MinRating = 0.1 },
                new MediaSortOption(MediaSortField.MyRating, Descending: true));
            Fill(HighestRated, rated.Take(RowSize));
            HasHighestRated = HighestRated.Count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load the Home page.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task Refresh() => LoadAsync();

    [RelayCommand]
    private void OpenMedia(MediaCardViewModel? card)
    {
        if (card is not null)
        {
            _navigation.NavigateToDetail<MediaDetailViewModel>(vm => vm.MediaId = card.Id);
        }
    }

    private void Fill(ObservableCollection<MediaCardViewModel> target, System.Collections.Generic.IEnumerable<Media> media)
    {
        target.Clear();
        foreach (var item in media)
        {
            target.Add(_cards.Create(item));
        }
    }
}
