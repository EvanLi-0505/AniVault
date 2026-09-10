using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AniVault.Models;

namespace AniVault.Metadata.Providers;

/// <summary>
/// Jikan (https://jikan.moe) — the unofficial read-only MyAnimeList API. Anime only, no API key.
/// Good English + Japanese titles and MAL scores; usually reachable without a VPN.
/// </summary>
public sealed partial class JikanProvider : IMetadataProvider
{
    private const string Api = "https://api.jikan.moe/v4";

    private readonly ProviderHttp _http;

    public JikanProvider(IHttpClientFactory httpClientFactory)
    {
        _http = new ProviderHttp(httpClientFactory, DisplayName);
    }

    public ExternalSource Source => ExternalSource.Jikan;

    public string DisplayName => "Jikan (MyAnimeList)";

    public string Description => "Anime only. No API key. MyAnimeList data — English/Japanese titles and scores; usually works without a VPN.";

    public bool RequiresApiKey => false;

    public bool SupportsMediaType(MediaType mediaType) => mediaType == MediaType.Anime;

    public async Task<IReadOnlyList<MetadataSearchResult>> SearchAsync(
        string query, MediaType? preferredMediaType, CancellationToken cancellationToken)
    {
        var url = $"{Api}/anime?q={Uri.EscapeDataString(query)}&limit=20&sfw=true";
        using var doc = await _http.GetJsonAsync(url, cancellationToken);

        var results = new List<MetadataSearchResult>();
        if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            return results;
        }

        foreach (var item in data.EnumerateArray())
        {
            results.Add(new MetadataSearchResult
            {
                Source = Source,
                ExternalId = item.GetInt("mal_id")?.ToString() ?? string.Empty,
                MediaType = MediaType.Anime,
                Title = FirstNonEmpty(item.GetString("title_english"), item.GetString("title"), item.GetString("title_japanese")) ?? "(untitled)",
                SecondaryTitle = item.GetString("title_japanese") ?? item.GetString("title"),
                Year = item.GetInt("year") ?? MetadataParsing.ParseDate(AiredFrom(item))?.Year,
                PosterUrl = ReadImage(item),
                Summary = Truncate(MetadataParsing.CleanText(item.GetString("synopsis")), 200),
            });
        }

        return results;
    }

    public async Task<MediaMetadata?> GetDetailsAsync(string externalId, CancellationToken cancellationToken)
    {
        if (!int.TryParse(externalId, out var id))
        {
            return null;
        }

        using var doc = await _http.GetJsonAsync($"{Api}/anime/{id}/full", cancellationToken);
        if (!doc.RootElement.TryGetProperty("data", out var m) || m.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var start = MetadataParsing.ParseDate(AiredFrom(m));
        var end = MetadataParsing.ParseDate(DatePart(m.TryGetProperty("aired", out var aired) ? aired.GetString("to") : null));

        var genres = new List<string>();
        foreach (var key in new[] { "genres", "themes", "demographics" })
        {
            if (m.TryGetProperty(key, out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                genres.AddRange(arr.EnumerateArray().Select(g => g.GetString("name")).Where(n => !string.IsNullOrWhiteSpace(n))!);
            }
        }

        var score = m.GetDouble("score");

        return new MediaMetadata
        {
            Source = Source,
            ExternalId = id.ToString(),
            MediaType = MediaType.Anime,
            Title = FirstNonEmpty(m.GetString("title_english"), m.GetString("title"), m.GetString("title_japanese")) ?? "(untitled)",
            OriginalTitle = m.GetString("title_japanese"),
            AlternativeTitles = ReadSynonyms(m),
            Description = MetadataParsing.CleanText(m.GetString("synopsis")),
            StartDate = start,
            EndDate = end,
            AirYear = m.GetInt("year") ?? start?.Year,
            AirSeason = MetadataParsing.ParseAnimeSeason(m.GetString("season")) ?? MetadataParsing.YearAndSeason(start).Season,
            EpisodeCount = m.GetInt("episodes"),
            RuntimeMinutes = ParseDurationMinutes(m.GetString("duration")),
            OfficialWebsite = m.GetString("url"),
            PosterUrl = ReadImage(m),
            ProviderRating = score is > 0 ? score : null,
            Genres = genres.Distinct(StringComparer.OrdinalIgnoreCase).Take(10).ToList(),
        };
    }

    private static string? AiredFrom(JsonElement item)
        => DatePart(item.TryGetProperty("aired", out var aired) ? aired.GetString("from") : null);

    /// <summary>Jikan dates are ISO timestamps ("2023-09-29T00:00:00+00:00"); keep just the date.</summary>
    private static string? DatePart(string? value)
        => string.IsNullOrEmpty(value) ? value : value.Split('T', 2)[0];

    private static IReadOnlyList<string> ReadSynonyms(JsonElement m)
        => m.TryGetProperty("title_synonyms", out var s) && s.ValueKind == JsonValueKind.Array
            ? s.EnumerateArray().Select(x => x.GetString()).Where(x => !string.IsNullOrWhiteSpace(x)).ToList()!
            : Array.Empty<string>();

    private static string? ReadImage(JsonElement item)
    {
        if (!item.TryGetProperty("images", out var images) || images.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var format in new[] { "webp", "jpg" })
        {
            if (images.TryGetProperty(format, out var f) && f.ValueKind == JsonValueKind.Object)
            {
                var url = f.GetString("large_image_url") ?? f.GetString("image_url");
                if (!string.IsNullOrWhiteSpace(url))
                {
                    return url;
                }
            }
        }

        return null;
    }

    private static int? ParseDurationMinutes(string? duration)
    {
        if (string.IsNullOrWhiteSpace(duration))
        {
            return null;
        }

        var match = DurationRegex().Match(duration);
        if (!match.Success)
        {
            return null;
        }

        var minutes = 0;
        if (match.Groups["hr"].Success)
        {
            minutes += int.Parse(match.Groups["hr"].Value) * 60;
        }

        if (match.Groups["min"].Success)
        {
            minutes += int.Parse(match.Groups["min"].Value);
        }

        return minutes > 0 ? minutes : null;
    }

    [GeneratedRegex(@"(?:(?<hr>\d+)\s*hr)?\s*(?:(?<min>\d+)\s*min)?", RegexOptions.IgnoreCase)]
    private static partial Regex DurationRegex();

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    private static string? Truncate(string? text, int max)
        => text is null || text.Length <= max ? text : text[..max].TrimEnd() + "…";
}
