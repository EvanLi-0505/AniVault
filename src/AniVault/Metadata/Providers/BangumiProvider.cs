using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AniVault.Models;

namespace AniVault.Metadata.Providers;

/// <summary>
/// Bangumi (https://bgm.tv) via its v0 API. Chinese-language titles and summaries; no API key.
/// Note: the API host can be unreachable from mainland China without a VPN.
/// </summary>
public sealed class BangumiProvider : IMetadataProvider
{
    private const string Api = "https://api.bgm.tv";

    // Bangumi subject types: 2 = anime, 6 = real (live-action film / TV).
    private const int TypeAnime = 2;
    private const int TypeReal = 6;

    private readonly ProviderHttp _http;

    public BangumiProvider(IHttpClientFactory httpClientFactory)
    {
        _http = new ProviderHttp(httpClientFactory, DisplayName);
    }

    public ExternalSource Source => ExternalSource.Bangumi;

    public string DisplayName => "Bangumi";

    public string Description => "Anime & live-action, Chinese titles. No API key. May require a VPN in mainland China.";

    public bool RequiresApiKey => false;

    public bool SupportsMediaType(MediaType mediaType) => true;

    public async Task<IReadOnlyList<MetadataSearchResult>> SearchAsync(
        string query, MediaType? preferredMediaType, CancellationToken cancellationToken)
    {
        var type = preferredMediaType is MediaType.Movie or MediaType.TvSeries ? TypeReal : TypeAnime;
        var body = new { keyword = query, filter = new { type = new[] { type } } };

        using var doc = await _http.PostJsonAsync($"{Api}/v0/search/subjects?limit=20", body, cancellationToken);

        var results = new List<MetadataSearchResult>();
        if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            return results;
        }

        foreach (var item in data.EnumerateArray())
        {
            var date = MetadataParsing.ParseDate(item.GetString("date"));
            results.Add(new MetadataSearchResult
            {
                Source = Source,
                ExternalId = item.GetInt("id")?.ToString() ?? string.Empty,
                MediaType = MapType(item.GetInt("type") ?? type, preferredMediaType),
                Title = FirstNonEmpty(item.GetString("name_cn"), item.GetString("name")) ?? "(untitled)",
                SecondaryTitle = item.GetString("name"),
                Year = date?.Year,
                PosterUrl = ReadImage(item, "common"),
                Summary = Truncate(MetadataParsing.CleanText(item.GetString("summary")), 200),
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

        using var doc = await _http.GetJsonAsync($"{Api}/v0/subjects/{id}", cancellationToken);
        var s = doc.RootElement;
        if (s.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var subjectType = s.GetInt("type") ?? TypeAnime;
        var mediaType = MapType(subjectType, null);
        var start = MetadataParsing.ParseDate(s.GetString("date"));
        var (airYear, airSeason) = MetadataParsing.YearAndSeason(start);

        var genres = new List<string>();
        if (s.TryGetProperty("tags", out var tags) && tags.ValueKind == JsonValueKind.Array)
        {
            genres.AddRange(tags.EnumerateArray()
                .Select(t => t.GetString("name"))
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Take(8)!);
        }

        var episodeCount = s.GetInt("eps") ?? s.GetInt("total_episodes");
        double? rating = s.TryGetProperty("rating", out var r) ? r.GetDouble("score") : null;

        return new MediaMetadata
        {
            Source = Source,
            ExternalId = id.ToString(),
            MediaType = mediaType,
            Title = FirstNonEmpty(s.GetString("name_cn"), s.GetString("name")) ?? "(untitled)",
            OriginalTitle = s.GetString("name"),
            Description = MetadataParsing.CleanText(s.GetString("summary")),
            StartDate = start,
            AirYear = airYear,
            AirSeason = mediaType == MediaType.Anime ? airSeason : null,
            EpisodeCount = episodeCount,
            PosterUrl = ReadImage(s, "large"),
            ProviderRating = rating is > 0 ? rating : null,
            Genres = genres,
            OfficialWebsite = $"https://bgm.tv/subject/{id}",
        };
    }

    private static MediaType MapType(int bangumiType, MediaType? preferred) => bangumiType switch
    {
        TypeAnime => MediaType.Anime,
        TypeReal => preferred == MediaType.Movie ? MediaType.Movie : MediaType.TvSeries,
        _ => preferred ?? MediaType.Anime,
    };

    private static string? ReadImage(JsonElement element, string size)
        => element.TryGetProperty("images", out var images) && images.ValueKind == JsonValueKind.Object
            ? images.GetString(size) ?? images.GetString("large") ?? images.GetString("common")
            : null;

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    private static string? Truncate(string? text, int max)
        => text is null || text.Length <= max ? text : text[..max].TrimEnd() + "…";
}
