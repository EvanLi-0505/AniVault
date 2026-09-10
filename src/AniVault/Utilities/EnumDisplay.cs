using AniVault.Models;
using AniVault.Services;

namespace AniVault.Utilities;

/// <summary>
/// Turns domain enums into localized user-facing labels. The label text comes from
/// <see cref="LocalizationService"/> (keys "Status.*", "Type.*", "Season.*", "Sort.*"),
/// falling back to English if the service isn't ready.
/// </summary>
public static class EnumDisplay
{
    public static string Label(WatchStatus status) => Get($"Status.{status}", status switch
    {
        WatchStatus.Planned => "\U0001F4E5 Planned / Want to Watch",
        WatchStatus.Watching => "▶ Watching",
        WatchStatus.Completed => "✓ Completed",
        WatchStatus.OnHold => "⏸ On Hold",
        WatchStatus.Dropped => "✕ Dropped",
        _ => status.ToString(),
    });

    public static string ShortLabel(WatchStatus status) => Get($"Status.Short.{status}", status.ToString());

    public static string Label(MediaType type) => Get($"Type.{type}", type switch
    {
        MediaType.Anime => "Anime",
        MediaType.Movie => "Movie",
        MediaType.TvSeries => "TV Series",
        _ => type.ToString(),
    });

    public static string PluralLabel(MediaType type) => Get($"Type.Plural.{type}", type switch
    {
        MediaType.Movie => "Movies",
        MediaType.TvSeries => "TV Series",
        _ => "Anime",
    });

    public static string Label(AnimeSeason season) => Get($"Season.{season}", season.ToString());

    public static string Label(MediaSortField field) => Get($"Sort.{field}", field switch
    {
        MediaSortField.Title => "Title",
        MediaSortField.MyRating => "My rating",
        MediaSortField.BroadcastDate => "Broadcast date",
        MediaSortField.AddedDate => "Date added",
        MediaSortField.UpdatedDate => "Last updated",
        MediaSortField.CompletedDate => "Completion date",
        MediaSortField.EpisodeCount => "Episode count",
        _ => field.ToString(),
    });

    private static string Get(string key, string fallback)
    {
        var text = LocalizationService.Instance?.Text(key);
        return string.IsNullOrEmpty(text) || text == key ? fallback : text;
    }
}
