using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    private readonly ISettingsService _settings;
    private readonly IDialogService _dialogService;
    private readonly IMediaCardFactory _cards;
    private readonly IArtworkService _artwork;
    private readonly INavigationService _navigation;
    private readonly ILocalizationService _loc;
    private readonly ILogger<LibraryViewModel> _logger;

    private const int PageSize = 60;

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

    [ObservableProperty] private bool _ratingGuideVisible;
    [ObservableProperty] private string _ratingGuideText = string.Empty;

    public LibraryViewModel(
        IMediaQueryService queryService,
        IMediaService mediaService,
        IMediaEditorService editorService,
        IOnlineSearchService onlineSearch,
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
        if (preset.ShowRatingGuide && string.IsNullOrEmpty(RatingGuideText))
        {
            RatingGuideText = RatingGuide.Text;
        }

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
        Filters.EndUpdate();
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
        await Filters.LoadOptionsAsync(_preset.MediaType);

        if (_preset.TagId is { } tagId)
        {
            Filters.BeginUpdate();
            foreach (var option in Filters.Tags)
            {
                option.IsSelected = option.Id == tagId;
            }

            Filters.EndUpdate();
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

        Items.Clear();
        foreach (var media in _pageSource.Skip((CurrentPage - 1) * PageSize).Take(PageSize))
        {
            Items.Add(_cards.Create(media));
        }

        IsEmpty = _pageSource.Count == 0;
        UpdateHeaderCount();
        PrevPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
    }
}
