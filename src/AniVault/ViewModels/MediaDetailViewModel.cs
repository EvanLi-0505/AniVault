using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using AniVault.Models;
using AniVault.Services;
using AniVault.Services.Artwork;
using AniVault.Utilities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AniVault.ViewModels;

/// <summary>A tag reference shown on the detail page.</summary>
public sealed record TagLink(int Id, string Name);

/// <summary>
/// The media detail page: full metadata, quick personal-data toggles, and an episode
/// checklist. Every action works offline and writes straight to the local database.
/// </summary>
public sealed partial class MediaDetailViewModel : ViewModelBase
{
    private readonly IMediaService _mediaService;
    private readonly IMediaEditorService _editorService;
    private readonly IDialogService _dialogService;
    private readonly INavigationService _navigation;
    private readonly IArtworkService _artwork;
    private readonly Metadata.IMetadataService _metadata;
    private readonly Metadata.IMetadataImporter _metadataImporter;
    private readonly ISettingsService _settings;
    private readonly ILocalizationService _loc;
    private readonly ILogger<MediaDetailViewModel> _logger;

    private Media? _media;
    private bool _applyingModel;

    [ObservableProperty] private int _mediaId;
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string? _originalTitle;
    [ObservableProperty] private string? _description;
    [ObservableProperty] private string? _notes;
    [ObservableProperty] private string _typeLabel = string.Empty;
    [ObservableProperty] private string? _broadcastLabel;
    [ObservableProperty] private string? _dateRangeLabel;
    [ObservableProperty] private string? _runtimeLabel;
    [ObservableProperty] private string? _countryLabel;
    [ObservableProperty] private string? _website;
    [ObservableProperty] private bool _hasTags;
    [ObservableProperty] private double? _myRating;
    [ObservableProperty] private WatchStatus _status;
    [ObservableProperty] private bool _isFavorite;
    [ObservableProperty] private bool _isLiked;
    [ObservableProperty] private ImageSource? _poster;
    [ObservableProperty] private ImageSource? _backdrop;
    [ObservableProperty] private bool _hasBackdrop;
    [ObservableProperty] private string _episodeProgressLabel = string.Empty;
    [ObservableProperty] private bool _hasEpisodes;
    [ObservableProperty] private bool _canGenerateEpisodes;
    [ObservableProperty] private bool _showCompleteSuggestion;
    [ObservableProperty] private bool _canRefreshMetadata;
    [ObservableProperty] private bool _isRefreshingMetadata;

    public MediaDetailViewModel(
        IMediaService mediaService,
        IMediaEditorService editorService,
        IDialogService dialogService,
        INavigationService navigation,
        IArtworkService artwork,
        Metadata.IMetadataService metadata,
        Metadata.IMetadataImporter metadataImporter,
        ISettingsService settings,
        ILocalizationService loc,
        ILogger<MediaDetailViewModel> logger)
    {
        _mediaService = mediaService;
        _editorService = editorService;
        _dialogService = dialogService;
        _navigation = navigation;
        _loc = loc;
        _artwork = artwork;
        _metadata = metadata;
        _metadataImporter = metadataImporter;
        _settings = settings;
        _logger = logger;
    }

    public ObservableCollection<EpisodeRowViewModel> Episodes { get; } = new();

    /// <summary>Tag chips; clicking one opens a filtered view.</summary>
    public ObservableCollection<TagLink> Tags { get; } = new();

    public WatchStatus[] StatusOptions { get; } = Enum.GetValues<WatchStatus>();

    public bool HasWebsite => !string.IsNullOrWhiteSpace(Website);

    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

    public bool HasNotes => !string.IsNullOrWhiteSpace(Notes);

    public override async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            _media = await _mediaService.GetByIdAsync(MediaId);
            if (_media is null)
            {
                _dialogService.ShowError(_loc.Text("Detail.NotFound"));
                _navigation.GoBack();
                return;
            }

            ApplyModel(_media);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load media detail {MediaId}.", MediaId);
            _dialogService.ShowError(_loc.Text("Detail.OpenFailed"));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyModel(Media media)
    {
        _applyingModel = true;

        Title = media.Title;
        OriginalTitle = string.Equals(media.OriginalTitle, media.Title, StringComparison.Ordinal) ? null : media.OriginalTitle;
        Description = media.Description;
        Notes = media.Notes;
        TypeLabel = _loc.Format("Detail.TypeFormat", EnumDisplay.Label(media.MediaType));

        var broadcast = FormatBroadcast(media);
        BroadcastLabel = broadcast is null ? null : _loc.Format("Detail.BroadcastFormat", broadcast);

        var range = FormatDateRange(media);
        DateRangeLabel = range is null ? null : _loc.Format("Detail.DatesFormat", range);

        RuntimeLabel = media.RuntimeMinutes is { } r and > 0
            ? _loc.Format("Detail.RuntimeFormat", _loc.Format("Detail.RuntimeMinFormat", r))
            : null;
        CountryLabel = media.Country is { } c ? _loc.Format("Detail.CountryFormat", c) : null;
        Website = media.OfficialWebsite;
        Tags.Clear();
        foreach (var mt in media.MediaTags.Where(mt => mt.Tag is not null).OrderBy(mt => mt.Tag!.Name))
        {
            Tags.Add(new TagLink(mt.TagId, mt.Tag!.Name));
        }

        HasTags = Tags.Count > 0;
        MyRating = media.MyRating;
        Status = media.Status;
        IsFavorite = media.IsFavorite;
        IsLiked = media.IsLiked;

        OnPropertyChanged(nameof(HasWebsite));
        OnPropertyChanged(nameof(HasDescription));
        OnPropertyChanged(nameof(HasNotes));

        Episodes.Clear();
        foreach (var episode in media.Episodes.OrderBy(e => e.EpisodeNumber))
        {
            Episodes.Add(new EpisodeRowViewModel(episode, PersistEpisodeWatchedAsync));
        }

        var watched = media.Episodes.Count(e => e.IsWatched);
        var total = media.EpisodeCount ?? media.Episodes.Count;
        HasEpisodes = media.Episodes.Count > 0;
        CanGenerateEpisodes = !HasEpisodes && media.EpisodeCount is > 0;
        EpisodeProgressLabel = total > 0
            ? _loc.Format("Detail.EpisodeProgressFormat", watched, total)
            : _loc.Text("Detail.NoEpisodeList");
        ShowCompleteSuggestion = HasEpisodes && watched == media.Episodes.Count && media.Status != WatchStatus.Completed;

        _ = RefreshCanRefreshAsync(media);
        _ = LoadArtworkAsync(media);

        _applyingModel = false;
    }

    private async Task LoadArtworkAsync(Media media)
    {
        Poster = await ImageLoading.LoadAsync(_artwork.GetPosterPath(media), decodePixelWidth: 500);

        var backdrop = await ImageLoading.LoadAsync(_artwork.GetBackdropPath(media), decodePixelWidth: 1280);
        Backdrop = backdrop;
        HasBackdrop = backdrop is not null;
    }

    partial void OnStatusChanged(WatchStatus value)
    {
        if (_applyingModel || _media is null)
        {
            return;
        }

        _ = RunAsync(async () =>
        {
            await _mediaService.SetStatusAsync(_media.Id, value);
            await ReloadAsync();
        });
    }

    partial void OnMyRatingChanged(double? value)
    {
        if (_applyingModel || _media is null)
        {
            return;
        }

        _ = RunAsync(() => _mediaService.SetRatingAsync(_media.Id, value));
    }

    [RelayCommand]
    private void GoBack() => _navigation.GoBack();

    private async Task RefreshCanRefreshAsync(Media media)
    {
        var hasExternalId = media.ExternalIds.Count > 0;
        var onlineEnabled = await _settings.GetBoolAsync(SettingKeys.OnlineSearchEnabled, false);
        CanRefreshMetadata = hasExternalId && onlineEnabled;
    }

    [RelayCommand]
    private async Task RefreshMetadata()
    {
        if (_media is null || _media.ExternalIds.Count == 0)
        {
            return;
        }

        if (!_dialogService.Confirm(_loc.Text("Detail.RefreshConfirm"), _loc.Text("Detail.RefreshConfirmTitle")))
        {
            return;
        }

        var externalId = _media.ExternalIds[0];
        IsRefreshingMetadata = true;
        try
        {
            var metadata = await _metadata.GetDetailsAsync(externalId.Source, externalId.ExternalId, System.Threading.CancellationToken.None);
            if (metadata is null)
            {
                _dialogService.ShowWarning(_loc.Text("Detail.RefreshNoData"));
                return;
            }

            await _metadataImporter.RefreshAsync(_media.Id, metadata);
            await ReloadAsync();
            _dialogService.ShowInfo(_loc.Text("Detail.RefreshDone"));
        }
        catch (Metadata.MetadataProviderException ex)
        {
            _dialogService.ShowWarning(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Metadata refresh failed for media {MediaId}.", _media.Id);
            _dialogService.ShowError(_loc.Text("Detail.RefreshFailed"));
        }
        finally
        {
            IsRefreshingMetadata = false;
        }
    }

    [RelayCommand]
    private void OpenTag(TagLink? tag)
    {
        if (tag is not null)
        {
            _navigation.NavigateToDetail<LibraryViewModel>(vm => vm.Configure(new LibraryPreset(
                $"Tag: {tag.Name}", "Every media item with this tag.", TagId: tag.Id)));
        }
    }

    [RelayCommand]
    private async Task ToggleFavorite()
    {
        if (_media is null)
        {
            return;
        }

        IsFavorite = !IsFavorite;
        await _mediaService.SetFavoriteAsync(_media.Id, IsFavorite);
    }

    [RelayCommand]
    private async Task ToggleLiked()
    {
        if (_media is null)
        {
            return;
        }

        IsLiked = !IsLiked;
        await _mediaService.SetLikedAsync(_media.Id, IsLiked);
    }

    [RelayCommand]
    private async Task Edit()
    {
        if (_media is not null && await _editorService.EditAsync(_media.Id))
        {
            await ReloadAsync();
        }
    }

    [RelayCommand]
    private async Task Delete()
    {
        if (_media is null)
        {
            return;
        }

        if (!_dialogService.Confirm(_loc.Format("Dialog.DeleteMediaFormat", _media.Title), _loc.Text("Dialog.DeleteMediaTitle")))
        {
            return;
        }

        await _mediaService.DeleteAsync(_media.Id);
        await _artwork.DeleteAllArtworkAsync(_media.Id, _media.MediaType);
        _navigation.GoBack();
    }

    [RelayCommand]
    private async Task GenerateEpisodes()
    {
        if (_media?.EpisodeCount is not { } count || count <= 0)
        {
            return;
        }

        await _mediaService.SyncEpisodeListAsync(_media.Id, count);
        await ReloadAsync();
    }

    [RelayCommand]
    private async Task MarkCompleted()
    {
        if (_media is null)
        {
            return;
        }

        await _mediaService.SetStatusAsync(_media.Id, WatchStatus.Completed);
        await ReloadAsync();
    }

    [RelayCommand]
    private void OpenWebsite()
    {
        if (string.IsNullOrWhiteSpace(Website))
        {
            return;
        }

        if (!Uri.TryCreate(Website, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            _dialogService.ShowWarning(_loc.Text("Detail.BadWebsite"));
            return;
        }

        // Opens in the user's normal browser — AniVault never embeds web content.
        Process.Start(new ProcessStartInfo { FileName = uri.ToString(), UseShellExecute = true });
    }

    private async Task ReloadAsync()
    {
        _media = await _mediaService.GetByIdAsync(MediaId);
        if (_media is not null)
        {
            ApplyModel(_media);
        }
    }

    private async Task PersistEpisodeWatchedAsync(int episodeId, bool isWatched)
    {
        try
        {
            await _mediaService.SetEpisodeWatchedAsync(episodeId, isWatched);
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update episode {EpisodeId}.", episodeId);
        }
    }

    private async Task RunAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Detail page action failed for media {MediaId}.", MediaId);
            _dialogService.ShowError(_loc.Text("Detail.SaveActionFailed"));
        }
    }

    private static string? FormatBroadcast(Media media)
    {
        if (media.MediaType == MediaType.Anime)
        {
            if (media.AirYear is { } year)
            {
                return media.AirSeason is { } season ? $"{year} · {EnumDisplay.Label(season)}" : year.ToString();
            }

            return media.AirSeason is { } onlySeason ? EnumDisplay.Label(onlySeason) : null;
        }

        return media.AirYear?.ToString();
    }

    private static string? FormatDateRange(Media media)
    {
        static string? Fmt(DateOnly? d) => d?.ToString("yyyy-MM-dd");
        var start = Fmt(media.StartDate);
        var end = Fmt(media.EndDate);
        return (start, end) switch
        {
            (null, null) => null,
            (not null, null) => start,
            (null, not null) => $"→ {end}",
            _ => $"{start} → {end}",
        };
    }
}
