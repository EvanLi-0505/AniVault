using System;
using System.Collections.Generic;
using System.Linq;
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
/// Backs the modal "Add / Edit media" window. Works completely offline: the user types
/// everything by hand and may optionally pick a local image file as the poster.
/// </summary>
public sealed partial class MediaEditorViewModel : ObservableValidator
{
    private readonly IMediaService _mediaService;
    private readonly ITagService _tagService;
    private readonly IArtworkService _artwork;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _loc;
    private readonly ILogger<MediaEditorViewModel> _logger;

    private int? _editingId;
    private string? _pickedPosterSourcePath;
    private string? _pickedBackdropSourcePath;
    private bool _hadPoster;
    private bool _hadBackdrop;

    [ObservableProperty]
    private string _windowTitle = "Add media";

    [ObservableProperty]
    private MediaType _mediaType = MediaType.Anime;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Title is required.")]
    private string _titleText = string.Empty;

    [ObservableProperty]
    private string? _originalTitle;

    [ObservableProperty]
    private string? _alternativeTitles;

    [ObservableProperty]
    private string? _description;

    [ObservableProperty]
    private int? _airYear;

    [ObservableProperty]
    private AnimeSeason? _airSeason;

    [ObservableProperty]
    private int? _airMonth;

    [ObservableProperty]
    private DateTime? _startDate;

    [ObservableProperty]
    private DateTime? _endDate;

    [ObservableProperty]
    private int? _episodeCount;

    [ObservableProperty]
    private int? _runtimeMinutes;

    [ObservableProperty]
    private string? _country;

    [ObservableProperty]
    private string? _officialWebsite;

    [ObservableProperty]
    private WatchStatus _status = WatchStatus.Planned;

    [ObservableProperty]
    private double? _myRating;

    [ObservableProperty]
    private bool _isFavorite;

    [ObservableProperty]
    private bool _isLiked;

    [ObservableProperty]
    private string? _notes;

    [ObservableProperty]
    private string? _posterDisplayPath;

    [ObservableProperty]
    private string? _backdropDisplayPath;

    public MediaEditorViewModel(
        IMediaService mediaService,
        ITagService tagService,
        IArtworkService artwork,
        IDialogService dialogService,
        ILocalizationService loc,
        TagPickerViewModel tags,
        ILogger<MediaEditorViewModel> logger)
    {
        _mediaService = mediaService;
        _tagService = tagService;
        _artwork = artwork;
        _dialogService = dialogService;
        _loc = loc;
        Tags = tags;
        _logger = logger;
    }

    /// <summary>Raised when the window should close. Argument is the dialog result (true = saved).</summary>
    public event Action<bool>? RequestClose;

    /// <summary>Tag editor component.</summary>
    public TagPickerViewModel Tags { get; }

    public IReadOnlyList<WatchStatus> WatchStatusOptions { get; } =
        Enum.GetValues<WatchStatus>();

    /// <summary>
    /// Every category, always editable. A metadata provider's own category guess can be wrong
    /// (e.g. Bangumi's "real" subject type covers both live-action movies and TV dramas, so an
    /// imported movie can land here typed as a TV series) — this lets the user correct it by hand
    /// instead of being stuck with whatever the provider decided.
    /// </summary>
    public IReadOnlyList<MediaType> MediaTypeOptions { get; } = Enum.GetValues<MediaType>();

    public IReadOnlyList<AnimeSeason?> AnimeSeasonOptions { get; } =
        new AnimeSeason?[] { null }.Concat(Enum.GetValues<AnimeSeason>().Cast<AnimeSeason?>()).ToList();

    public bool ShowAnimeFields => MediaType == MediaType.Anime;

    public bool ShowRuntimeField => MediaType == MediaType.Movie;

    partial void OnMediaTypeChanged(MediaType value)
    {
        OnPropertyChanged(nameof(ShowAnimeFields));
        OnPropertyChanged(nameof(ShowRuntimeField));
    }

    /// <summary>Prepares the editor to create a new item of the given type.</summary>
    public async Task InitializeForNewAsync(MediaType mediaType)
    {
        _editingId = null;
        MediaType = mediaType;
        WindowTitle = _loc.Format("Editor.AddFormat", EnumDisplay.Label(mediaType));
        await Tags.LoadAsync();
    }

    /// <summary>Loads an existing item into the editor.</summary>
    public async Task<bool> InitializeForEditAsync(int mediaId)
    {
        var media = await _mediaService.GetByIdAsync(mediaId);
        if (media is null)
        {
            _dialogService.ShowError(_loc.Text("Editor.NotFound"));
            return false;
        }

        _editingId = media.Id;
        MediaType = media.MediaType;
        WindowTitle = _loc.Format("Editor.EditFormat", media.Title);

        TitleText = media.Title;
        OriginalTitle = media.OriginalTitle;
        AlternativeTitles = media.AlternativeTitles;
        Description = media.Description;
        AirYear = media.AirYear;
        AirSeason = media.AirSeason;
        AirMonth = media.AirMonth;
        StartDate = ToDateTime(media.StartDate);
        EndDate = ToDateTime(media.EndDate);
        EpisodeCount = media.EpisodeCount;
        RuntimeMinutes = media.RuntimeMinutes;
        Country = media.Country;
        OfficialWebsite = media.OfficialWebsite;
        Status = media.Status;
        MyRating = media.MyRating;
        IsFavorite = media.IsFavorite;
        IsLiked = media.IsLiked;
        Notes = media.Notes;
        PosterDisplayPath = _artwork.GetPosterPath(media);
        BackdropDisplayPath = _artwork.GetBackdropPath(media);
        _hadPoster = media.PosterPath is not null;
        _hadBackdrop = media.BackdropPath is not null;
        await Tags.LoadAsync(media.MediaTags.Select(mt => mt.Tag?.Name ?? string.Empty));
        return true;
    }

    [RelayCommand]
    private void BrowsePoster()
    {
        var image = PickImage(_loc.Text("Editor.PickPosterTitle"));
        if (image is not null)
        {
            _pickedPosterSourcePath = image;
            PosterDisplayPath = image;
        }
    }

    [RelayCommand]
    private void ClearPoster()
    {
        _pickedPosterSourcePath = null;
        PosterDisplayPath = null;
    }

    [RelayCommand]
    private void BrowseBackdrop()
    {
        var image = PickImage(_loc.Text("Editor.PickBackdropTitle"));
        if (image is not null)
        {
            _pickedBackdropSourcePath = image;
            BackdropDisplayPath = image;
        }
    }

    [RelayCommand]
    private void ClearBackdrop()
    {
        _pickedBackdropSourcePath = null;
        BackdropDisplayPath = null;
    }

    private string? PickImage(string title) => _dialogService.PickFile(
        title, "Images (*.jpg;*.jpeg;*.png;*.webp;*.bmp)|*.jpg;*.jpeg;*.png;*.webp;*.bmp");

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(false);

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task Save()
    {
        ValidateAllProperties();
        if (HasErrors)
        {
            return;
        }

        try
        {
            var media = _editingId is null
                ? new Media()
                : await _mediaService.GetByIdAsync(_editingId.Value) ?? new Media();

            var previousType = media.MediaType;
            var categoryChanged = _editingId is not null && previousType != MediaType;

            media.MediaType = MediaType;
            media.Title = TitleText.Trim();
            media.OriginalTitle = Trimmed(OriginalTitle);
            media.AlternativeTitles = Trimmed(AlternativeTitles);
            media.Description = Trimmed(Description);
            media.AirYear = AirYear;
            media.AirSeason = MediaType == MediaType.Anime ? AirSeason : null;
            media.AirMonth = AirMonth is >= 1 and <= 12 ? AirMonth : null;
            media.StartDate = ToDateOnly(StartDate);
            media.EndDate = ToDateOnly(EndDate);
            media.EpisodeCount = EpisodeCount;
            media.RuntimeMinutes = MediaType == MediaType.Movie ? RuntimeMinutes : media.RuntimeMinutes;
            media.Country = Trimmed(Country);
            media.OfficialWebsite = Trimmed(OfficialWebsite);
            media.Status = Status;
            media.MyRating = ClampRating(MyRating);
            media.IsFavorite = IsFavorite;
            media.IsLiked = IsLiked;
            media.Notes = Trimmed(Notes);
            if (Status == WatchStatus.Completed && media.CompletedAt is null)
            {
                media.CompletedAt = DateTime.UtcNow;
            }

            if (_editingId is null)
            {
                await _mediaService.CreateAsync(media);
            }
            else
            {
                await _mediaService.UpdateAsync(media);
            }

            if (categoryChanged)
            {
                await _artwork.RelocateArtworkAsync(media.Id, previousType, MediaType);
            }

            await _tagService.SetMediaTagsAsync(media.Id, Tags.GetTags());
            await ApplyArtworkAsync(media.Id);

            RequestClose?.Invoke(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save media \"{Title}\".", TitleText);
            _dialogService.ShowError(_loc.Text("Editor.SaveFailed"));
        }
    }

    private bool CanSave() => !string.IsNullOrWhiteSpace(TitleText);

    /// <summary>Applies the poster/backdrop changes after the media row exists (so it has an id).</summary>
    private async Task ApplyArtworkAsync(int mediaId)
    {
        try
        {
            if (_pickedPosterSourcePath is not null)
            {
                await _artwork.SetPosterAsync(mediaId, _pickedPosterSourcePath);
            }
            else if (_hadPoster && PosterDisplayPath is null)
            {
                await _artwork.ClearPosterAsync(mediaId);
            }

            if (_pickedBackdropSourcePath is not null)
            {
                await _artwork.SetBackdropAsync(mediaId, _pickedBackdropSourcePath);
            }
            else if (_hadBackdrop && BackdropDisplayPath is null)
            {
                await _artwork.ClearBackdropAsync(mediaId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Saved media {MediaId} but an artwork change failed.", mediaId);
            _dialogService.ShowWarning(_loc.Text("Editor.ArtworkFailed"));
        }
    }

    private static string? Trimmed(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static double? ClampRating(double? rating)
        => rating is null ? null : Math.Clamp(rating.Value, 0d, 10d);

    private static DateTime? ToDateTime(DateOnly? date)
        => date is null ? null : date.Value.ToDateTime(TimeOnly.MinValue);

    private static DateOnly? ToDateOnly(DateTime? date)
        => date is null ? null : DateOnly.FromDateTime(date.Value);
}
