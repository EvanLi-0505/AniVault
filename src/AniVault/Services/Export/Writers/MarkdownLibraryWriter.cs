using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using AniVault.Models;
using AniVault.Utilities;

namespace AniVault.Services.Export.Writers;

/// <summary>
/// Renders the library as a single Markdown document: a summary table, then one section per
/// media type, split into watch-status groups so "watched" and "still to watch" are obvious.
/// Pure function of its input — easy to unit test.
/// </summary>
public sealed class MarkdownLibraryWriter
{
    // The order status groups appear within each media-type section.
    private static readonly WatchStatus[] StatusOrder =
    {
        WatchStatus.Completed,
        WatchStatus.Watching,
        WatchStatus.OnHold,
        WatchStatus.Planned,
        WatchStatus.Dropped,
    };

    public string Write(IReadOnlyList<Media> allMedia)
    {
        var sb = new StringBuilder();
        var generatedAt = DateTime.Now;

        sb.AppendLine("# AniVault Library Export").AppendLine();
        sb.Append("_Generated ")
          .Append(generatedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture))
          .Append(" · ").Append(allMedia.Count).Append(" item(s) · ")
          .Append(allMedia.Count(m => m.Status == WatchStatus.Completed)).Append(" completed_")
          .AppendLine().AppendLine();

        WriteSummaryTable(sb, allMedia);

        foreach (var type in Enum.GetValues<MediaType>())
        {
            var itemsOfType = allMedia.Where(m => m.MediaType == type).ToList();
            if (itemsOfType.Count == 0)
            {
                continue;
            }

            sb.AppendLine().Append("## ").AppendLine(PluralLabel(type)).AppendLine();

            foreach (var status in StatusOrder)
            {
                var group = itemsOfType
                    .Where(m => m.Status == status)
                    .OrderBy(m => m.Title, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();
                if (group.Count == 0)
                {
                    continue;
                }

                sb.Append("### ").Append(EnumDisplay.Label(status)).Append(" (").Append(group.Count).Append(')').AppendLine().AppendLine();

                foreach (var media in group)
                {
                    WriteMediaEntry(sb, media);
                }
            }
        }

        return sb.ToString();
    }

    private static void WriteSummaryTable(StringBuilder sb, IReadOnlyList<Media> allMedia)
    {
        sb.AppendLine("## Summary").AppendLine();
        sb.AppendLine("| Type | Total | Completed | Watching | On hold | Planned | Dropped |");
        sb.AppendLine("|---|---:|---:|---:|---:|---:|---:|");

        foreach (var type in Enum.GetValues<MediaType>())
        {
            var items = allMedia.Where(m => m.MediaType == type).ToList();
            sb.Append("| ").Append(PluralLabel(type))
              .Append(" | ").Append(items.Count)
              .Append(" | ").Append(items.Count(m => m.Status == WatchStatus.Completed))
              .Append(" | ").Append(items.Count(m => m.Status == WatchStatus.Watching))
              .Append(" | ").Append(items.Count(m => m.Status == WatchStatus.OnHold))
              .Append(" | ").Append(items.Count(m => m.Status == WatchStatus.Planned))
              .Append(" | ").Append(items.Count(m => m.Status == WatchStatus.Dropped))
              .AppendLine(" |");
        }

        sb.AppendLine();
    }

    private static void WriteMediaEntry(StringBuilder sb, Media media)
    {
        var heading = media.Title;
        if (!string.IsNullOrWhiteSpace(media.OriginalTitle) &&
            !string.Equals(media.OriginalTitle, media.Title, StringComparison.Ordinal))
        {
            heading += $" — {media.OriginalTitle}";
        }

        sb.Append("#### ").AppendLine(heading).AppendLine();

        AddFact(sb, "Status", EnumDisplay.Label(media.Status));
        AddFact(sb, "My rating", media.MyRating is { } r ? $"★ {r.ToString("0.0", CultureInfo.InvariantCulture)} / 10" : null);

        if (media.MediaType == MediaType.Anime)
        {
            AddFact(sb, "Broadcast", FormatSeason(media));
        }
        else if (media.AirYear is { } year)
        {
            AddFact(sb, "Year", year.ToString());
        }

        AddFact(sb, "Episodes watched", FormatEpisodeProgress(media));
        if (media.RuntimeMinutes is { } runtime and > 0)
        {
            AddFact(sb, "Runtime", $"{runtime} min");
        }

        var flags = new List<string>();
        if (media.IsFavorite) flags.Add("Favorite");
        if (media.IsLiked) flags.Add("Liked");
        AddFact(sb, "Marks", flags.Count > 0 ? string.Join(" · ", flags) : null);

        var tags = media.MediaTags
            .Select(mt => mt.Tag?.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList();
        AddFact(sb, "Tags", tags.Count > 0 ? string.Join(", ", tags) : null);

        AddFact(sb, "Dates", FormatDateRange(media));
        AddFact(sb, "Completed on", media.CompletedAt?.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        AddFact(sb, "Country", media.Country);
        AddFact(sb, "Website", media.OfficialWebsite);

        foreach (var external in media.ExternalIds)
        {
            AddFact(sb, external.Source.ToString() + " ID", external.ExternalId);
        }

        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(media.Description))
        {
            AppendBlockQuote(sb, media.Description!);
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(media.Notes))
        {
            sb.AppendLine("**Notes:**").AppendLine();
            AppendBlockQuote(sb, media.Notes!);
            sb.AppendLine();
        }

        sb.AppendLine("---").AppendLine();
    }

    private static void AddFact(StringBuilder sb, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            sb.Append("- **").Append(label).Append(":** ").AppendLine(value.Replace("\r", string.Empty).Replace("\n", " "));
        }
    }

    private static string FormatEpisodeProgress(Media media)
    {
        var watched = media.Episodes.Count(e => e.IsWatched);
        var total = media.EpisodeCount ?? (media.Episodes.Count > 0 ? media.Episodes.Count : 0);

        if (total == 0 && watched == 0)
        {
            return media.Status == WatchStatus.Completed ? "all" : "0";
        }

        return total > 0 ? $"{watched} / {total}" : watched.ToString();
    }

    private static string? FormatSeason(Media media)
    {
        if (media.AirYear is not { } year)
        {
            return media.AirSeason is { } onlySeason ? EnumDisplay.Label(onlySeason) : null;
        }

        return media.AirSeason is { } season ? $"{year} · {EnumDisplay.Label(season)}" : year.ToString();
    }

    private static string? FormatDateRange(Media media)
    {
        static string? Fmt(DateOnly? d) => d?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

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

    private static void AppendBlockQuote(StringBuilder sb, string text)
    {
        foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
        {
            sb.Append("> ").AppendLine(line);
        }
    }

    private static string PluralLabel(MediaType type) => type switch
    {
        MediaType.Anime => "Anime",
        MediaType.Movie => "Movies",
        MediaType.TvSeries => "TV Series",
        _ => type.ToString(),
    };
}
