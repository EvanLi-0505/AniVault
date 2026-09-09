using System.Collections.Generic;
using AniVault.Models;
using AniVault.Services;

namespace AniVault.Utilities;

/// <summary>
/// Central place that turns domain enums into user-facing labels.
/// Keeping these here (rather than scattered through XAML/C#) makes future
/// localization — including Chinese — a single-file change.
/// </summary>
public static class EnumDisplay
{
    public static IReadOnlyDictionary<WatchStatus, string> WatchStatusLabels { get; } =
        new Dictionary<WatchStatus, string>
        {
            [WatchStatus.Planned] = "\U0001F4E5 Planned / Want to Watch",
            [WatchStatus.Watching] = "▶ Watching",
            [WatchStatus.Completed] = "✓ Completed",
            [WatchStatus.OnHold] = "⏸ On Hold",
            [WatchStatus.Dropped] = "✕ Dropped",
        };

    public static IReadOnlyDictionary<MediaType, string> MediaTypeLabels { get; } =
        new Dictionary<MediaType, string>
        {
            [MediaType.Anime] = "Anime",
            [MediaType.Movie] = "Movie",
            [MediaType.TvSeries] = "TV Series",
        };

    public static IReadOnlyDictionary<AnimeSeason, string> AnimeSeasonLabels { get; } =
        new Dictionary<AnimeSeason, string>
        {
            [AnimeSeason.Winter] = "Winter",
            [AnimeSeason.Spring] = "Spring",
            [AnimeSeason.Summer] = "Summer",
            [AnimeSeason.Fall] = "Fall",
        };

    public static IReadOnlyDictionary<MediaSortField, string> SortFieldLabels { get; } =
        new Dictionary<MediaSortField, string>
        {
            [MediaSortField.Title] = "Title",
            [MediaSortField.MyRating] = "My rating",
            [MediaSortField.BroadcastDate] = "Broadcast date",
            [MediaSortField.AddedDate] = "Date added",
            [MediaSortField.UpdatedDate] = "Last updated",
            [MediaSortField.CompletedDate] = "Completion date",
            [MediaSortField.EpisodeCount] = "Episode count",
        };

    public static string Label(WatchStatus status) => WatchStatusLabels[status];

    public static string Label(MediaType type) => MediaTypeLabels[type];

    public static string Label(AnimeSeason season) => AnimeSeasonLabels[season];

    public static string Label(MediaSortField field) => SortFieldLabels[field];
}
