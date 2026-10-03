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
/// A pickable dropdown value with its own display label. Used instead of a bare <c>null</c>
/// item so WPF's <see cref="System.Windows.Controls.ComboBox"/> can actually re-select the
/// "All" / "(none)" entry (a null item can be chosen once but never re-selected).
/// </summary>
public sealed class FilterChoice<T> where T : struct
{
    public FilterChoice(T? value, string label)
    {
        Value = value;
        Label = label;
    }

    public T? Value { get; }

    public string Label { get; }

    public override string ToString() => Label;
}

/// <summary>
/// Reusable filter + sort panel. Shared by every browse surface (library pages, Search,
/// the sidebar shortcuts). The host calls <see cref="BuildFilter"/> / <see cref="BuildSort"/>
/// and listens to <see cref="Changed"/>.
/// </summary>
public sealed partial class FilterPanelViewModel : ObservableObject
{
    private const int TagsPerPage = 30;

    private readonly IMediaQueryService _queryService;
    private readonly ITagService _tagService;
    private readonly ISettingsService _settings;
    private readonly ILocalizationService _loc;
    private bool _suspend;

    [ObservableProperty] private string _text = string.Empty;
    [ObservableProperty] private FilterChoice<WatchStatus> _statusChoice = null!;
    [ObservableProperty] private FilterChoice<int> _yearChoice = null!;
    [ObservableProperty] private FilterChoice<AnimeSeason> _seasonChoice = null!;
    [ObservableProperty] private string _monthText = string.Empty;
    [ObservableProperty] private bool _favoriteOnly;
    [ObservableProperty] private bool _likedOnly;
    [ObservableProperty] private double? _minRating;
    [ObservableProperty] private bool _matchAllTags = true;
    [ObservableProperty] private MediaSortField _sortField = MediaSortField.UpdatedDate;
    [ObservableProperty] private bool _sortDescending = true;

    [ObservableProperty] private int _tagPage = 1;
    [ObservableProperty] private int _tagTotalPages = 1;
    [ObservableProperty] private bool _tagPagingVisible;

    // Host-controlled visibility. These only hide UI rows whose value the surface already
    // fixes; BuildFilter still emits the underlying value.
    [ObservableProperty] private bool _showStatus = true;
    [ObservableProperty] private bool _showFavorite = true;
    [ObservableProperty] private bool _showLiked = true;
    [ObservableProperty] private bool _showSeason = true;

    /// <summary>
    /// Whether the panel is folded down to just its show/hide button. A view preference, not a
    /// filter: collapsing never changes what <see cref="BuildFilter"/> emits, and it is remembered
    /// app-wide (every browse page shares the one setting) rather than per page.
    /// </summary>
    [ObservableProperty] private bool _isCollapsed;

    public FilterPanelViewModel(
        IMediaQueryService queryService, ITagService tagService, ISettingsService settings, ILocalizationService loc)
    {
        _queryService = queryService;
        _tagService = tagService;
        _settings = settings;
        _loc = loc;

        _suspend = true;

        StatusOptions = new ObservableCollection<FilterChoice<WatchStatus>>(
            new[] { new FilterChoice<WatchStatus>(null, _loc.Text("Common.Any")) }
                .Concat(Enum.GetValues<WatchStatus>().Select(s => new FilterChoice<WatchStatus>(s, EnumDisplay.Label(s)))));

        SeasonOptions = new ObservableCollection<FilterChoice<AnimeSeason>>(
            new[] { new FilterChoice<AnimeSeason>(null, _loc.Text("Common.None")) }
                .Concat(Enum.GetValues<AnimeSeason>().Select(s => new FilterChoice<AnimeSeason>(s, EnumDisplay.Label(s)))));

        YearOptions = new ObservableCollection<FilterChoice<int>> { new(null, _loc.Text("Common.Any")) };

        MonthOptions = new ObservableCollection<string>(
            new[] { _loc.Text("Common.Any") }
                .Concat(new[] { 1, 4, 7, 10 }.Select(m => _loc.Format("Filter.MonthFormat", m))));

        StatusChoice = StatusOptions[0];
        SeasonChoice = SeasonOptions[0];
        YearChoice = YearOptions[0];
        MonthText = MonthOptions[0];

        _suspend = false;
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

    /// <summary>
    /// Resumes notifications WITHOUT firing <see cref="Changed"/>. For a caller (like
    /// <c>LibraryViewModel.Configure</c>) that is about to do its own explicit, immediate reload
    /// right after — firing <see cref="Changed"/> here would only schedule a second, fully
    /// redundant reload ~200ms later (the debounce delay), which on a library page with many
    /// cards means paying the WPF layout/render cost for that page twice on every navigation.
    /// </summary>
    public void EndUpdateSilently() => _suspend = false;

    public ObservableCollection<FilterChoice<WatchStatus>> StatusOptions { get; }

    public ObservableCollection<FilterChoice<AnimeSeason>> SeasonOptions { get; }

    public ObservableCollection<FilterChoice<int>> YearOptions { get; }

    /// <summary>Preset choices for the editable month box (quarterly anime premiere months).</summary>
    public ObservableCollection<string> MonthOptions { get; }

    public ObservableCollection<TagFilterOption> Tags { get; } = new();

    /// <summary>The current page of <see cref="Tags"/> shown in the panel.</summary>
    public ObservableCollection<TagFilterOption> VisibleTags { get; } = new();

    public MediaSortField[] SortFields { get; } = Enum.GetValues<MediaSortField>();

    // ---- Nullable values external code (presets) reads / writes -----------------------------

    public WatchStatus? Status
    {
        get => StatusChoice?.Value;
        set => StatusChoice = StatusOptions.FirstOrDefault(c => Nullable.Equals(c.Value, value)) ?? StatusOptions[0];
    }

    public int? Year
    {
        get => YearChoice?.Value;
        set => YearChoice = YearOptions.FirstOrDefault(c => Nullable.Equals(c.Value, value)) ?? YearOptions[0];
    }

    public AnimeSeason? Season
    {
        get => SeasonChoice?.Value;
        set => SeasonChoice = SeasonOptions.FirstOrDefault(c => Nullable.Equals(c.Value, value)) ?? SeasonOptions[0];
    }

    /// <summary>The broadcast/release month (1-12) typed or picked in <see cref="MonthText"/>, or null.</summary>
    public int? Month
    {
        get => ParseMonth(MonthText);
        set => MonthText = value is { } m ? _loc.Format("Filter.MonthFormat", m) : MonthOptions[0];
    }

    public int SelectedTagCount => Tags.Count(t => t.IsSelected);

    public string TagPageLabel => _loc.Format("Filter.PageFormat", TagPage, TagTotalPages);

    public bool HasActiveFilters =>
        !string.IsNullOrWhiteSpace(Text) || Status is not null || Year is not null || Season is not null
        || Month is not null || FavoriteOnly || LikedOnly || MinRating is not null || Tags.Any(t => t.IsSelected);

    /// <summary>Pulls the first 1-12 number out of free-typed text ("4月", "Month 4", "4" all work).</summary>
    private static int? ParseMonth(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var digits = new string(text.Where(char.IsAsciiDigit).ToArray());
        return int.TryParse(digits, out var month) && month is >= 1 and <= 12 ? month : null;
    }

    public async Task LoadOptionsAsync(MediaType? mediaType)
    {
        _suspend = true;

        IsCollapsed = await _settings.GetBoolAsync(SettingKeys.FilterPanelCollapsed, false);

        var keepYear = Year;
        YearOptions.Clear();
        YearOptions.Add(new FilterChoice<int>(null, _loc.Text("Common.Any")));
        foreach (var year in await _queryService.GetBroadcastYearsAsync(mediaType))
        {
            YearOptions.Add(new FilterChoice<int>(year, year.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        YearChoice = YearOptions.FirstOrDefault(c => Nullable.Equals(c.Value, keepYear)) ?? YearOptions[0];

        var selectedTagIds = Tags.Where(t => t.IsSelected).Select(t => t.Id).ToHashSet();
        Tags.Clear();
        foreach (var tag in await _tagService.GetAllWithUsageAsync())
        {
            Tags.Add(new TagFilterOption(tag.Id, $"{tag.Name} ({tag.MediaCount})", RaiseChanged)
            {
                IsSelected = selectedTagIds.Contains(tag.Id),
            });
        }

        TagPage = 1;
        ApplyTagPage();

        _suspend = false;
    }

    public MediaFilter BuildFilter(MediaType? mediaType) => new()
    {
        MediaType = mediaType,
        Text = string.IsNullOrWhiteSpace(Text) ? null : Text.Trim(),
        Status = Status,
        Year = Year,
        Season = Season,
        Month = Month,
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
        StatusChoice = StatusOptions[0];
        YearChoice = YearOptions[0];
        SeasonChoice = SeasonOptions[0];
        MonthText = MonthOptions[0];
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

    [RelayCommand]
    private async Task ToggleCollapsed()
    {
        IsCollapsed = !IsCollapsed;
        await _settings.SetAsync(SettingKeys.FilterPanelCollapsed, IsCollapsed.ToString());
    }

    [RelayCommand(CanExecute = nameof(CanPrevTagPage))]
    private void PrevTagPage()
    {
        TagPage--;
        ApplyTagPage();
    }

    [RelayCommand(CanExecute = nameof(CanNextTagPage))]
    private void NextTagPage()
    {
        TagPage++;
        ApplyTagPage();
    }

    private bool CanPrevTagPage() => TagPage > 1;

    private bool CanNextTagPage() => TagPage < TagTotalPages;

    private void ApplyTagPage()
    {
        TagTotalPages = Math.Max(1, (int)Math.Ceiling(Tags.Count / (double)TagsPerPage));
        TagPage = Math.Clamp(TagPage, 1, TagTotalPages);
        TagPagingVisible = TagTotalPages > 1;

        VisibleTags.Clear();
        foreach (var tag in Tags.Skip((TagPage - 1) * TagsPerPage).Take(TagsPerPage))
        {
            VisibleTags.Add(tag);
        }

        OnPropertyChanged(nameof(TagPageLabel));
        OnPropertyChanged(nameof(SelectedTagCount));
        PrevTagPageCommand.NotifyCanExecuteChanged();
        NextTagPageCommand.NotifyCanExecuteChanged();
    }

    // Any tracked property changing notifies the host (and refreshes HasActiveFilters).
    partial void OnTextChanged(string value) => RaiseChanged();

    partial void OnStatusChoiceChanged(FilterChoice<WatchStatus> value)
    {
        OnPropertyChanged(nameof(Status));
        RaiseChanged();
    }

    partial void OnYearChoiceChanged(FilterChoice<int> value)
    {
        OnPropertyChanged(nameof(Year));
        RaiseChanged();
    }

    partial void OnSeasonChoiceChanged(FilterChoice<AnimeSeason> value)
    {
        OnPropertyChanged(nameof(Season));
        RaiseChanged();
    }

    partial void OnMonthTextChanged(string value)
    {
        OnPropertyChanged(nameof(Month));
        RaiseChanged();
    }

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
        OnPropertyChanged(nameof(SelectedTagCount));
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
