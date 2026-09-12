using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AniVault.Models;
using AniVault.Services;
using AniVault.Services.Artwork;
using AniVault.Utilities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AniVault.ViewModels;

/// <summary>
/// Describes what a <see cref="LibraryViewModel"/> instance should show. Used both for the
/// three library pages and for the sidebar shortcuts (a status, Favorites, Liked, by rating…).
/// </summary>
public sealed record LibraryPreset(
    string Title,
    string Subtitle,
    MediaType? MediaType = null,
    WatchStatus? Status = null,
    bool FavoriteOnly = false,
    bool LikedOnly = false,
    int? TagId = null,
    MediaSortField SortField = MediaSortField.UpdatedDate,
    bool SortDescending = true,
    bool ShowRatingGuide = false);

/// <summary>
/// A browsable list of media with the shared filter + sort panel. Configured by a
/// <see cref="LibraryPreset"/>. Fully offline; every query goes through <see cref="IMediaQueryService"/>.
/// </summary>
public sealed partial class LibraryViewModel : ViewModelBase
{
    private readonly IMediaQueryService _queryService;
    private readonly IMediaService _mediaService;
    private readonly IMediaEditorService _editorService;
    private readonly IOnlineSearchService _onlineSearch;
    private readonly IRatingGuideService _ratingGuide;
    private readonly ISettingsService _settings;
    private readonly IDialogService _dialogService;
    private readonly IMediaCardFactory _cards;
    private readonly IArtworkService _artwork;
    private readonly INavigationService _navigation;
    private readonly ILocalizationService _loc;
    private readonly ILogger<LibraryViewModel> _logger;

    // Lowered from 60: the library grid isn't virtualized, so WPF's render cost scales roughly
    // with card count (measured ~150-270ms for 47 cards vs ~20-45ms for 5) — a smaller page bounds
    // that cost without the risk of a hand-written virtualizing panel.
    private const int PageSize = 35;

    private LibraryPreset _preset = new("Library", string.Empty);
    private CancellationTokenSource? _reloadCts;
    private List<Media> _pageSource = new();

    [ObservableProperty] private string _title = "Library";
    [ObservableProperty] private string _subtitle = string.Empty;
    [ObservableProperty] private string _headerCountText = string.Empty;
    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private bool _canAdd;
    [ObservableProperty] private bool _showSeasonBrowser;
    [ObservableProperty] private bool _onlineSearchEnabled;
    [ObservableProperty] private string _addButtonText = "+ Add media";

    [ObservableProperty] private int _currentPage = 1;
    [ObservableProperty] private int _totalPages = 1;
    [ObservableProperty] private bool _pagingVisible;
    [ObservableProperty] private string _pageLabel = string.Empty;
    [ObservableProperty] private string _pageJumpText = string.Empty;

    [ObservableProperty] private bool _ratingGuideVisible;

    public LibraryViewModel(
        IMediaQueryService queryService,
        IMediaService mediaService,
        IMediaEditorService editorService,
        IOnlineSearchService onlineSearch,
        IRatingGuideService ratingGuide,
        ISettingsService settings,
        IDialogService dialogService,
        IMediaCardFactory cards,
        IArtworkService artwork,
        INavigationService navigation,
        ILocalizationService loc,
        FilterPanelViewModel filters,
        ILogger<LibraryViewModel> logger)
    {
        _queryService = queryService;
        _mediaService = mediaService;
        _editorService = editorService;
        _onlineSearch = onlineSearch;
        _ratingGuide = ratingGuide;
        _settings = settings;
        _dialogService = dialogService;
        _cards = cards;
        _artwork = artwork;
        _navigation = navigation;
        _loc = loc;
        Filters = filters;
        _logger = logger;

        Filters.Changed += (_, _) => ScheduleReload();
    }

    public FilterPanelViewModel Filters { get; }

    public ObservableCollection<MediaCardViewModel> Items { get; } = new();

    public string EmptyStateText => Filters.HasActiveFilters
        ? _loc.Text("Library.EmptyFiltered")
        : _preset.MediaType is { } t
            ? _loc.Format("Library.EmptyMediaTypeFormat", EnumDisplay.Label(t))
            : _loc.Text("Library.EmptyGeneric");

    /// <summary>Applies a preset. Call before navigating; the page reloads in LoadAsync.</summary>
    public void Configure(LibraryPreset preset)
    {
        _preset = preset;
        Title = preset.Title;
        Subtitle = preset.Subtitle;
        CanAdd = preset.MediaType is not null;
        ShowSeasonBrowser = preset.MediaType == MediaType.Anime;
        AddButtonText = preset.MediaType is { } type
            ? _loc.Format("Library.AddFormat", EnumDisplay.Label(type))
            : _loc.Text("Library.AddGeneric");

        RatingGuideVisible = preset.ShowRatingGuide;

        UpdateHeaderCount();

        Filters.BeginUpdate();
        Filters.Reset();
        Filters.SortField = preset.SortField;
        Filters.SortDescending = preset.SortDescending;
        Filters.Status = preset.Status;
        Filters.FavoriteOnly = preset.FavoriteOnly;
        Filters.LikedOnly = preset.LikedOnly;
        Filters.ShowStatus = preset.Status is null;
        Filters.ShowFavorite = !preset.FavoriteOnly;
        Filters.ShowLiked = !preset.LikedOnly;
        Filters.ShowSeason = preset.MediaType is null or MediaType.Anime;
        // LoadAsync() (called immediately after Configure by the navigation framework) does its
        // own explicit reload right away — EndUpdate()'s "fire Changed" would just schedule a
        // second, fully redundant reload ~200ms later via the debounced handler below.
        Filters.EndUpdateSilently();
    }

    /// <summary>Shorthand for the three library pages.</summary>
    public void SetMediaType(MediaType mediaType)
        => Configure(new LibraryPreset(
            _loc.Format("Library.TitleFormat", EnumDisplay.Label(mediaType)),
            _loc.Text("Library.DefaultSubtitle"),
            MediaType: mediaType));

    private void UpdateHeaderCount()
        => HeaderCountText = _loc.Format("Library.ItemsShownFormat", _pageSource.Count, Subtitle);

    public override async Task LoadAsync()
    {
        OnlineSearchEnabled = await _settings.GetBoolAsync(SettingKeys.OnlineSearchEnabled, false);

        var filterSw = Stopwatch.StartNew();
        await Filters.LoadOptionsAsync(_preset.MediaType);
        filterSw.Stop();
        _logger.LogInformation(
            "Loaded filter options ({TagCount} tag(s)) for '{Title}' in {ElapsedMs} ms.",
            Filters.Tags.Count, Title, filterSw.ElapsedMilliseconds);

        if (_preset.TagId is { } tagId)
        {
            Filters.BeginUpdate();
            foreach (var option in Filters.Tags)
            {
                option.IsSelected = option.Id == tagId;
            }

            // Same reasoning as Configure(): ReloadAsync() a few lines below already reloads
            // explicitly with this tag pre-selected, so firing Changed here would only schedule a
            // second, redundant reload.
            Filters.EndUpdateSilently();
        }

        await ReloadAsync(CancellationToken.None);
    }

    [RelayCommand]
    private async Task AddMedia()
    {
        if (_preset.MediaType is { } type && await _editorService.AddNewAsync(type))
        {
            await LoadAsync();
        }
    }

    [RelayCommand]
    private void OpenMedia(MediaCardViewModel? card)
    {
        if (card is not null)
        {
            _navigation.NavigateToDetail<MediaDetailViewModel>(vm => vm.MediaId = card.Id);
        }
    }

    [RelayCommand]
    private void BrowseSeasons() => _navigation.NavigateToDetail<SeasonsViewModel>();

    [RelayCommand]
    private void OpenRatingGuide() => _ratingGuide.Show();

    [RelayCommand(CanExecute = nameof(CanPrevPage))]
    private void PrevPage()
    {
        CurrentPage--;
        ApplyPage();
    }

    [RelayCommand(CanExecute = nameof(CanNextPage))]
    private void NextPage()
    {
        CurrentPage++;
        ApplyPage();
    }

    private bool CanPrevPage() => CurrentPage > 1;

    private bool CanNextPage() => CurrentPage < TotalPages;

    /// <summary>Jumps straight to the page typed into <see cref="PageJumpText"/>; ignores garbage/out-of-range input.</summary>
    [RelayCommand]
    private void GoToPage()
    {
        if (int.TryParse(PageJumpText, out var page) && page >= 1 && page <= TotalPages)
        {
            CurrentPage = page;
            ApplyPage();
        }

        PageJumpText = string.Empty;
    }

    [RelayCommand]
    private async Task SearchOnline()
    {
        if (!OnlineSearchEnabled)
        {
            if (!_dialogService.Confirm(_loc.Text("Online.EnablePrompt"), _loc.Text("Online.EnableTitle")))
            {
                return;
            }

            await _settings.SetAsync(SettingKeys.OnlineSearchEnabled, true.ToString());
            OnlineSearchEnabled = true;
        }

        var newId = await _onlineSearch.SearchAndImportAsync(_preset.MediaType);
        if (newId is { } id)
        {
            await LoadAsync();
            _navigation.NavigateToDetail<MediaDetailViewModel>(vm => vm.MediaId = id);
        }
    }

    [RelayCommand]
    private async Task EditMedia(MediaCardViewModel? card)
    {
        if (card is not null && await _editorService.EditAsync(card.Id))
        {
            await LoadAsync();
        }
    }

    [RelayCommand]
    private async Task DeleteMedia(MediaCardViewModel? card)
    {
        if (card is null)
        {
            return;
        }

        if (!_dialogService.Confirm(_loc.Format("Dialog.DeleteMediaFormat", card.Title), _loc.Text("Dialog.DeleteMediaTitle")))
        {
            return;
        }

        try
        {
            await _mediaService.DeleteAsync(card.Id);
            await _artwork.DeleteAllArtworkAsync(card.Id, card.MediaType);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete media {MediaId}.", card.Id);
            _dialogService.ShowError(_loc.Text("Dialog.DeleteFailed"));
        }
    }

    private void ScheduleReload()
    {
        OnPropertyChanged(nameof(EmptyStateText));
        _ = DebouncedReloadAsync();
    }

    private async Task DebouncedReloadAsync()
    {
        _reloadCts?.Cancel();
        var cts = new CancellationTokenSource();
        _reloadCts = cts;

        try
        {
            await Task.Delay(200, cts.Token);
            await ReloadAsync(cts.Token);
        }
        catch (TaskCanceledException)
        {
        }
    }

    private async Task ReloadAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        try
        {
            var results = await _queryService.QueryAsync(
                Filters.BuildFilter(_preset.MediaType),
                Filters.BuildSort(),
                cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            _pageSource = results.ToList();
            CurrentPage = 1;
            ApplyPage();
            OnPropertyChanged(nameof(EmptyStateText));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load the library view '{Title}'.", Title);
            _dialogService.ShowError(_loc.Text("Dialog.LoadLibraryFailed"));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyPage()
    {
        TotalPages = Math.Max(1, (int)Math.Ceiling(_pageSource.Count / (double)PageSize));
        CurrentPage = Math.Clamp(CurrentPage, 1, TotalPages);
        PagingVisible = TotalPages > 1;
        PageLabel = _loc.Format("Library.PageFormat", CurrentPage, TotalPages);

        var sw = Stopwatch.StartNew();
        Items.Clear();
        foreach (var media in _pageSource.Skip((CurrentPage - 1) * PageSize).Take(PageSize))
        {
            Items.Add(_cards.Create(media));
        }

        sw.Stop();
        _logger.LogInformation(
            "Built {Count} card(s) for '{Title}' in {ElapsedMs} ms (containers only, before layout/render).",
            Items.Count, Title, sw.ElapsedMilliseconds);

        // The line above only times how long it took to create and enqueue the card view models —
        // WPF's actual Measure/Arrange/Render pass for the WrapPanel runs later, asynchronously,
        // on the dispatcher. Schedule a continuation at Render priority (after layout, before the
        // UI goes idle) so we can see whether THAT pass is where the reported pause actually is.
        var renderSw = Stopwatch.StartNew();
        var renderedCount = Items.Count;
        var renderedTitle = Title;
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Render,
            new Action(() => _logger.LogInformation(
                "Rendered {Count} card(s) for '{Title}' in {ElapsedMs} ms (containers + layout + render).",
                renderedCount, renderedTitle, renderSw.ElapsedMilliseconds)));

        IsEmpty = _pageSource.Count == 0;
        UpdateHeaderCount();
        PrevPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
    }
}
