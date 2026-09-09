using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using AniVault.Models;
using AniVault.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AniVault.ViewModels;

/// <summary>One selectable tag in the filter panel.</summary>
public sealed partial class TagFilterOption : ObservableObject
{
    private readonly Action _onChanged;

    [ObservableProperty]
    private bool _isSelected;

    public TagFilterOption(int id, string name, Action onChanged)
    {
        Id = id;
        Name = name;
        _onChanged = onChanged;
    }

    public int Id { get; }

    public string Name { get; }

    partial void OnIsSelectedChanged(bool value) => _onChanged();
}

/// <summary>
/// Reusable filter + sort panel. Shared by every browse surface (library pages, Search,
/// the sidebar shortcuts). The host calls <see cref="BuildFilter"/> / <see cref="BuildSort"/>
/// and listens to <see cref="Changed"/>.
/// </summary>
public sealed partial class FilterPanelViewModel : ObservableObject
{
    private readonly IMediaQueryService _queryService;
    private readonly ITagService _tagService;
    private bool _suspend;

    [ObservableProperty] private string _text = string.Empty;
    [ObservableProperty] private WatchStatus? _status;
    [ObservableProperty] private int? _year;
    [ObservableProperty] private AnimeSeason? _season;
    [ObservableProperty] private bool _favoriteOnly;
    [ObservableProperty] private bool _likedOnly;
    [ObservableProperty] private double? _minRating;
    [ObservableProperty] private bool _matchAllTags = true;
    [ObservableProperty] private MediaSortField _sortField = MediaSortField.UpdatedDate;
    [ObservableProperty] private bool _sortDescending = true;

    // Host-controlled visibility. These only hide UI rows whose value the surface already
    // fixes; BuildFilter still emits the underlying value.
    [ObservableProperty] private bool _showStatus = true;
    [ObservableProperty] private bool _showFavorite = true;
    [ObservableProperty] private bool _showLiked = true;
    [ObservableProperty] private bool _showSeason = true;

    public FilterPanelViewModel(IMediaQueryService queryService, ITagService tagService)
    {
        _queryService = queryService;
        _tagService = tagService;
    }

    /// <summary>Raised whenever any filter or sort value changes.</summary>
    public event EventHandler? Changed;

    /// <summary>Suspends <see cref="Changed"/> while the host applies a preset in bulk.</summary>
    public void BeginUpdate() => _suspend = true;

    /// <summary>Resumes notifications and fires one <see cref="Changed"/>.</summary>
    public void EndUpdate()
    {
        _suspend = false;
        RaiseChanged();
    }

    public ObservableCollection<WatchStatus?> StatusOptions { get; } =
        new(new WatchStatus?[] { null }.Concat(Enum.GetValues<WatchStatus>().Cast<WatchStatus?>()));

    public ObservableCollection<AnimeSeason?> SeasonOptions { get; } =
        new(new AnimeSeason?[] { null }.Concat(Enum.GetValues<AnimeSeason>().Cast<AnimeSeason?>()));

    public ObservableCollection<int?> YearOptions { get; } = new();

    public ObservableCollection<TagFilterOption> Tags { get; } = new();

    public MediaSortField[] SortFields { get; } = Enum.GetValues<MediaSortField>();

    public bool HasActiveFilters =>
        !string.IsNullOrWhiteSpace(Text) || Status is not null || Year is not null || Season is not null
        || FavoriteOnly || LikedOnly || MinRating is not null || Tags.Any(t => t.IsSelected);

    public async Task LoadOptionsAsync(MediaType? mediaType)
    {
        _suspend = true;

        YearOptions.Clear();
        YearOptions.Add(null);
        foreach (var year in await _queryService.GetBroadcastYearsAsync(mediaType))
        {
            YearOptions.Add(year);
        }

        var selectedTagIds = Tags.Where(t => t.IsSelected).Select(t => t.Id).ToHashSet();
        Tags.Clear();
        foreach (var tag in await _tagService.GetAllWithUsageAsync())
        {
            Tags.Add(new TagFilterOption(tag.Id, $"{tag.Name} ({tag.MediaCount})", RaiseChanged)
            {
                IsSelected = selectedTagIds.Contains(tag.Id),
            });
        }

        _suspend = false;
    }

    public MediaFilter BuildFilter(MediaType? mediaType) => new()
    {
        MediaType = mediaType,
        Text = string.IsNullOrWhiteSpace(Text) ? null : Text.Trim(),
        Status = Status,
        Year = Year,
        Season = Season,
        IsFavorite = FavoriteOnly ? true : null,
        IsLiked = LikedOnly ? true : null,
        MinRating = MinRating,
        TagIds = Tags.Where(t => t.IsSelected).Select(t => t.Id).ToList(),
        MatchAllTags = MatchAllTags,
    };

    public MediaSortOption BuildSort() => new(SortField, SortDescending);

    [RelayCommand]
    public void Reset()
    {
        var wasSuspended = _suspend;
        _suspend = true;

        Text = string.Empty;
        Status = null;
        Year = null;
        Season = null;
        FavoriteOnly = false;
        LikedOnly = false;
        MinRating = null;
        MatchAllTags = true;
        foreach (var tag in Tags)
        {
            tag.IsSelected = false;
        }

        _suspend = wasSuspended;
        RaiseChanged();
    }

    // Any tracked property changing notifies the host (and refreshes HasActiveFilters).
    partial void OnTextChanged(string value) => RaiseChanged();
    partial void OnStatusChanged(WatchStatus? value) => RaiseChanged();
    partial void OnYearChanged(int? value) => RaiseChanged();
    partial void OnSeasonChanged(AnimeSeason? value) => RaiseChanged();
    partial void OnFavoriteOnlyChanged(bool value) => RaiseChanged();
    partial void OnLikedOnlyChanged(bool value) => RaiseChanged();
    partial void OnMinRatingChanged(double? value) => RaiseChanged();
    partial void OnMatchAllTagsChanged(bool value) => RaiseChanged();
    partial void OnSortFieldChanged(MediaSortField value) => RaiseChanged();
    partial void OnSortDescendingChanged(bool value) => RaiseChanged();

    private void RaiseChanged()
    {
        if (_suspend)
        {
            return;
        }

        OnPropertyChanged(nameof(HasActiveFilters));
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
