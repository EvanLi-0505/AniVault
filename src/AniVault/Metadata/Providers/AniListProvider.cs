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
/// AniList (https://anilist.co) via its public GraphQL API. Anime only, no API key.
/// Generally reachable from mainland China without a VPN, which makes it a good default.
/// </summary>
public sealed class AniListProvider : IMetadataProvider
{
    private const string Endpoint = "https://graphql.anilist.co";

    private const string SearchQuery = @"
query ($search: String) {
  Page(page: 1, perPage: 20) {
    media(search: $search, type: ANIME, sort: SEARCH_MATCH) {
      id
      seasonYear
      title { romaji english native }
      coverImage { medium }
      description(asHtml: false)
    }
  }
}";

    private const string DetailsQuery = @"
query ($id: Int) {
  Media(id: $id, type: ANIME) {
    id
    seasonYear
    season
    episodes
    duration
    countryOfOrigin
    averageScore
    siteUrl
    bannerImage
    title { romaji english native }
    synonyms
    coverImage { extraLarge large }
    description(asHtml: false)
    startDate { year month day }
    endDate { year month day }
    genres
  }
}";

    private readonly ProviderHttp _http;

    public AniListProvider(IHttpClientFactory httpClientFactory)
    {
        _http = new ProviderHttp(httpClientFactory, DisplayName);
    }

    public ExternalSource Source => ExternalSource.AniList;

    public string DisplayName => "AniList";

    public string Description => "Anime. No API key. Often reachable in mainland China without a VPN, but the API is sometimes down.";

    public bool RequiresApiKey => false;

    public bool SupportsMediaType(MediaType mediaType) => mediaType == MediaType.Anime;

    public async Task<IReadOnlyList<MetadataSearchResult>> SearchAsync(
        string query, MediaType? preferredMediaType, CancellationToken cancellationToken)
    {
        using var doc = await PostGraphQlAsync(SearchQuery, new { search = query }, cancellationToken);

        var results = new List<MetadataSearchResult>();
        if (!TryGetData(doc, out var data) ||
            !data.TryGetProperty("Page", out var page) ||
            !page.TryGetProperty("media", out var media))
        {
            return results;
        }

        foreach (var item in media.EnumerateArray())
        {
            var title = item.GetProperty("title");
            results.Add(new MetadataSearchResult
            {
                Source = Source,
                ExternalId = item.GetInt("id")?.ToString() ?? string.Empty,
                MediaType = MediaType.Anime,
                Title = title.GetString("romaji") ?? title.GetString("english") ?? title.GetString("native") ?? "(untitled)",
                SecondaryTitle = title.GetString("native"),
                Year = item.GetInt("seasonYear"),
                PosterUrl = item.TryGetProperty("coverImage", out var cover) ? cover.GetString("medium") : null,
                Summary = Truncate(MetadataParsing.CleanText(item.GetString("description")), 200),
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

        using var doc = await PostGraphQlAsync(DetailsQuery, new { id }, cancellationToken);
        if (!TryGetData(doc, out var data) || !data.TryGetProperty("Media", out var m) || m.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var title = m.GetProperty("title");
        var start = ReadFuzzyDate(m, "startDate");
        var end = ReadFuzzyDate(m, "endDate");

        var alternatives = new List<string>();
        if (m.TryGetProperty("synonyms", out var syn) && syn.ValueKind == JsonValueKind.Array)
        {
            alternatives.AddRange(syn.EnumerateArray().Select(s => s.GetString()).Where(s => !string.IsNullOrWhiteSpace(s))!);
        }

        var genres = m.TryGetProperty("genres", out var g) && g.ValueKind == JsonValueKind.Array
            ? g.EnumerateArray().Select(x => x.GetString()).Where(x => !string.IsNullOrWhiteSpace(x)).ToList()!
            : new List<string>();

        return new MediaMetadata
        {
            Source = Source,
            ExternalId = id.ToString(),
            MediaType = MediaType.Anime,
            Title = title.GetString("romaji") ?? title.GetString("english") ?? title.GetString("native") ?? "(untitled)",
            OriginalTitle = title.GetString("native"),
            AlternativeTitles = alternatives,
            Description = MetadataParsing.CleanText(m.GetString("description")),
            StartDate = start,
            EndDate = end,
            AirYear = m.GetInt("seasonYear") ?? start?.Year,
            AirSeason = MetadataParsing.ParseAnimeSeason(m.GetString("season"))
                        ?? (start is { } s ? MetadataParsing.YearAndSeason(s).Season : null),
            EpisodeCount = m.GetInt("episodes"),
            RuntimeMinutes = m.GetInt("duration"),
            Country = m.GetString("countryOfOrigin"),
            OfficialWebsite = m.GetString("siteUrl"),
            PosterUrl = m.TryGetProperty("coverImage", out var cover)
                ? cover.GetString("extraLarge") ?? cover.GetString("large")
                : null,
            BackdropUrl = m.GetString("bannerImage"),
            ProviderRating = m.GetInt("averageScore") is { } score ? score / 10d : null,
            Genres = genres,
        };
    }

    private async Task<JsonDocument> PostGraphQlAsync(string gql, object variables, CancellationToken cancellationToken)
        => await _http.PostJsonAsync(Endpoint, new { query = gql, variables }, cancellationToken);

    private static bool TryGetData(JsonDocument doc, out JsonElement data)
    {
        if (doc.RootElement.TryGetProperty("data", out data) && data.ValueKind == JsonValueKind.Object)
        {
            return true;
        }

        data = default;
        return false;
    }

    private static DateOnly? ReadFuzzyDate(JsonElement parent, string property)
    {
        if (!parent.TryGetProperty(property, out var d) || d.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var year = d.GetInt("year");
        if (year is null)
        {
            return null;
        }

        var month = Math.Clamp(d.GetInt("month") ?? 1, 1, 12);
        var day = d.GetInt("day") ?? 1;
        try
        {
            return new DateOnly(year.Value, month, Math.Clamp(day, 1, DateTime.DaysInMonth(year.Value, month)));
        }
        catch
        {
            return new DateOnly(year.Value, 1, 1);
        }
    }

    private static string? Truncate(string? text, int max)
        => text is null || text.Length <= max ? text : text[..max].TrimEnd() + "…";
}
