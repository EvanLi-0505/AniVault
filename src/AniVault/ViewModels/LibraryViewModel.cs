using System;
using System.Collections.ObjectModel;
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
    bool SortDescending = true);

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

    private LibraryPreset _preset = new("Library", string.Empty);
    private CancellationTokenSource? _reloadCts;

    [ObservableProperty] private string _title = "Library";
    [ObservableProperty] private string _subtitle = string.Empty;
    [ObservableProperty] private string _headerCountText = string.Empty;
    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private bool _canAdd;
    [ObservableProperty] private bool _showSeasonBrowser;
    [ObservableProperty] private bool _onlineSearchEnabled;
    [ObservableProperty] private string _addButtonText = "+ Add media";

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
        Items.CollectionChanged += (_, _) => UpdateHeaderCount();
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
        => HeaderCountText = _loc.Format("Library.ItemsShownFormat", Items.Count, Subtitle);

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

    [RelayCommand]
    private async Task SearchOnline()
    {
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

            Items.Clear();
            foreach (var media in results)
            {
                Items.Add(_cards.Create(media));
            }

            IsEmpty = Items.Count == 0;
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
}
