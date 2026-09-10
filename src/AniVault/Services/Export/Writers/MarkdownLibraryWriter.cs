using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using AniVault.Models;
using AniVault.Services;
using AniVault.Utilities;

namespace AniVault.Services.Export.Writers;

/// <summary>
/// Renders the library as one compact Markdown document: a small summary table, then a section
/// per media type, split into watch-status groups, one line per title (name, broadcast time,
/// personal rating, episode progress, marks, tags) plus optional original-title / notes lines.
/// Pure function of its input — easy to unit test. Headings and labels are localized through
/// <see cref="LocalizationService.Instance"/> (English when it isn't set, e.g. in tests).
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

        sb.Append("# ").AppendLine(L("Export.Title", "AniVault Library")).AppendLine();
        sb.AppendLine(Format(
            "Export.GeneratedFormat", "_Exported {0} · {1} items · {2} completed_",
            DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
            allMedia.Count,
            allMedia.Count(m => m.Status == WatchStatus.Completed)))
          .AppendLine();

        if (allMedia.Count == 0)
        {
            sb.AppendLine(L("Export.Empty", "_Your library is empty._"));
            return sb.ToString();
        }

        WriteSummaryTable(sb, allMedia);

        foreach (var type in Enum.GetValues<MediaType>())
        {
            var itemsOfType = allMedia.Where(m => m.MediaType == type).ToList();
            if (itemsOfType.Count == 0)
            {
                continue;
            }

            sb.AppendLine()
              .Append("## ").Append(EnumDisplay.PluralLabel(type))
              .Append(" (").Append(itemsOfType.Count).Append(')').AppendLine().AppendLine();

            foreach (var status in StatusOrder)
            {
                var group = itemsOfType
                    .Where(m => m.Status == status)
                    .OrderByDescending(m => m.MyRating ?? -1d)
                    .ThenBy(m => m.Title, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();
                if (group.Count == 0)
                {
                    continue;
                }

                sb.Append("### ").Append(EnumDisplay.Label(status))
                  .Append(" (").Append(group.Count).Append(')').AppendLine().AppendLine();

                foreach (var media in group)
                {
                    WriteMediaLine(sb, media);
                }

                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    private static void WriteSummaryTable(StringBuilder sb, IReadOnlyList<Media> allMedia)
    {
        sb.Append("## ").AppendLine(L("Export.SummaryHeading", "Summary")).AppendLine();
        sb.Append("| ").Append(L("Export.ColType", "Type"))
          .Append(" | ").Append(L("Export.ColTotal", "Total"))
          .Append(" | ").Append(EnumDisplay.ShortLabel(WatchStatus.Completed))
          .Append(" | ").Append(EnumDisplay.ShortLabel(WatchStatus.Watching))
          .Append(" | ").Append(EnumDisplay.ShortLabel(WatchStatus.OnHold))
          .Append(" | ").Append(EnumDisplay.ShortLabel(WatchStatus.Planned))
          .Append(" | ").Append(EnumDisplay.ShortLabel(WatchStatus.Dropped))
          .AppendLine(" |");
        sb.AppendLine("|---|---:|---:|---:|---:|---:|---:|");

        foreach (var type in Enum.GetValues<MediaType>())
        {
            var items = allMedia.Where(m => m.MediaType == type).ToList();
            sb.Append("| ").Append(EnumDisplay.PluralLabel(type))
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

    private static void WriteMediaLine(StringBuilder sb, Media media)
    {
        var facts = new List<string>();

        var when = FormatWhen(media);
        if (when is not null)
        {
            facts.Add(when);
        }

        if (media.MyRating is { } rating)
        {
            facts.Add("★" + rating.ToString("0.#", CultureInfo.InvariantCulture));
        }

        var progress = FormatEpisodeProgress(media);
        if (progress is not null)
        {
            facts.Add(progress);
        }

        if (media.MediaType == MediaType.Movie && media.RuntimeMinutes is { } runtime and > 0)
        {
            facts.Add(Format("Detail.RuntimeMinFormat", "{0} min", runtime));
        }

        var marks = string.Concat(media.IsFavorite ? "❤" : string.Empty, media.IsLiked ? "👍" : string.Empty);
        if (marks.Length > 0)
        {
            facts.Add(marks);
        }

        var tags = media.MediaTags
            .Select(mt => mt.Tag?.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList();
        if (tags.Count > 0)
        {
            facts.Add(string.Join(" / ", tags));
        }

        sb.Append("- **").Append(OneLine(media.Title)).Append("**");
        if (facts.Count > 0)
        {
            sb.Append(" — ").Append(string.Join("  ·  ", facts));
        }

        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(media.OriginalTitle) &&
            !string.Equals(media.OriginalTitle, media.Title, StringComparison.Ordinal))
        {
            sb.Append("  ").AppendLine(OneLine(media.OriginalTitle!));
        }

        if (!string.IsNullOrWhiteSpace(media.Notes))
        {
            sb.Append("  ").Append(L("Export.NotesLabel", "Notes: ")).AppendLine(OneLine(media.Notes!));
        }
    }

    private static string? FormatWhen(Media media)
    {
        if (media.MediaType == MediaType.Anime)
        {
            if (media.AirYear is { } y)
            {
                return media.AirSeason is { } s ? $"{y} {EnumDisplay.Label(s)}" : y.ToString(CultureInfo.InvariantCulture);
            }

            return media.AirSeason is { } onlySeason ? EnumDisplay.Label(onlySeason) : null;
        }

        if (media.AirYear is { } year)
        {
            return year.ToString(CultureInfo.InvariantCulture);
        }

        return media.StartDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static string? FormatEpisodeProgress(Media media)
    {
        if (media.MediaType == MediaType.Movie)
        {
            return null;
        }

        var watched = media.Episodes.Count(e => e.IsWatched);
        var total = media.EpisodeCount ?? (media.Episodes.Count > 0 ? media.Episodes.Count : 0);

        if (total <= 0)
        {
            return watched > 0 ? Format("Export.EpisodesProgressFormat", "{0}/{1} eps", watched, "?") : null;
        }

        if (media.Status == WatchStatus.Completed || watched >= total)
        {
            return Format("Export.EpisodesAllFormat", "{0} eps", total);
        }

        return Format("Export.EpisodesProgressFormat", "{0}/{1} eps", watched, total);
    }

    private static string OneLine(string value)
        => value.Replace("\r", string.Empty).Replace("\n", " ").Trim();

    private static string L(string key, string fallback)
    {
        var text = LocalizationService.Instance?.Text(key);
        return string.IsNullOrEmpty(text) || text == key ? fallback : text;
    }

    private static string Format(string key, string fallbackTemplate, params object?[] args)
    {
        var template = L(key, fallbackTemplate);
        try
        {
            return string.Format(CultureInfo.CurrentCulture, template, args);
        }
        catch (FormatException)
        {
            return string.Format(CultureInfo.CurrentCulture, fallbackTemplate, args);
        }
    }
}
