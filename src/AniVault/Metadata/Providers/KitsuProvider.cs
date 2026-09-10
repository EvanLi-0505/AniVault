using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AniVault.Models;

namespace AniVault.Metadata.Providers;

/// <summary>
/// Kitsu (https://kitsu.io) via its public JSON:API. Anime only, no API key. English and
/// romaji/Japanese titles; commonly reachable without a VPN.
/// </summary>
public sealed class KitsuProvider : IMetadataProvider
{
    private const string Api = "https://kitsu.io/api/edge";

    private readonly ProviderHttp _http;

    public KitsuProvider(IHttpClientFactory httpClientFactory)
    {
        _http = new ProviderHttp(httpClientFactory, DisplayName);
    }

    public ExternalSource Source => ExternalSource.Kitsu;

    public string DisplayName => "Kitsu";

    public string Description => "Anime only. No API key. English and romaji titles; usually works without a VPN.";

    public bool RequiresApiKey => false;

    public bool SupportsMediaType(MediaType mediaType) => mediaType == MediaType.Anime;

    public async Task<IReadOnlyList<MetadataSearchResult>> SearchAsync(
        string query, MediaType? preferredMediaType, CancellationToken cancellationToken)
    {
        var url = $"{Api}/anime?filter[text]={Uri.EscapeDataString(query)}&page[limit]=20";
        using var doc = await SendAsync(url, cancellationToken);

        var results = new List<MetadataSearchResult>();
        foreach (var item in JsonPath.SelectArray(doc.RootElement, "data"))
        {
            if (JsonPath.Select(item, "attributes") is not { } attr)
            {
                continue;
            }

            var start = MetadataParsing.ParseDate(attr.GetString("startDate"));
            results.Add(new MetadataSearchResult
            {
                Source = Source,
                ExternalId = item.GetString("id") ?? string.Empty,
                MediaType = MediaType.Anime,
                Title = ReadTitle(attr),
                SecondaryTitle = JsonPath.SelectString(attr, "titles.ja_jp") ?? JsonPath.SelectString(attr, "titles.en_jp"),
                Year = start?.Year,
                PosterUrl = JsonPath.SelectString(attr, "posterImage.medium") ?? JsonPath.SelectString(attr, "posterImage.small"),
                Summary = Truncate(MetadataParsing.CleanText(attr.GetString("synopsis")), 200),
            });
        }

        return results;
    }

    public async Task<MediaMetadata?> GetDetailsAsync(string externalId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(externalId))
        {
            return null;
        }

        using var doc = await SendAsync($"{Api}/anime/{Uri.EscapeDataString(externalId)}?include=genres,categories", cancellationToken);
        if (JsonPath.Select(doc.RootElement, "data.attributes") is not { } attr)
        {
            return null;
        }

        var start = MetadataParsing.ParseDate(attr.GetString("startDate"));
        var end = MetadataParsing.ParseDate(attr.GetString("endDate"));

        var genres = JsonPath.SelectArray(doc.RootElement, "included")
            .Where(x => x.GetString("type") is "genres" or "categories")
            .Select(x => JsonPath.SelectString(x, "attributes.name"))
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();

        var rating = JsonPath.SelectDouble(attr, "averageRating");

        return new MediaMetadata
        {
            Source = Source,
            ExternalId = externalId,
            MediaType = MediaType.Anime,
            Title = ReadTitle(attr),
            OriginalTitle = JsonPath.SelectString(attr, "titles.ja_jp"),
            Description = MetadataParsing.CleanText(attr.GetString("synopsis")),
            StartDate = start,
            EndDate = end,
            AirYear = start?.Year,
            AirSeason = MetadataParsing.YearAndSeason(start).Season,
            EpisodeCount = attr.GetInt("episodeCount"),
            RuntimeMinutes = attr.GetInt("episodeLength"),
            PosterUrl = JsonPath.SelectString(attr, "posterImage.original") ?? JsonPath.SelectString(attr, "posterImage.medium"),
            BackdropUrl = JsonPath.SelectString(attr, "coverImage.original"),
            ProviderRating = rating is > 0 ? Math.Round(rating.Value / 10d, 1) : null,
            Genres = genres,
        };
    }

    private Task<JsonDocument> SendAsync(string url, CancellationToken cancellationToken)
    {
        var request = _http.NewRequest(HttpMethod.Get, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.api+json"));
        return _http.SendAsync(request, cancellationToken);
    }

    private static string ReadTitle(JsonElement attr)
        => JsonPath.SelectString(attr, "titles.en")
           ?? JsonPath.SelectString(attr, "titles.en_jp")
           ?? attr.GetString("canonicalTitle")
           ?? JsonPath.SelectString(attr, "titles.ja_jp")
           ?? "(untitled)";

    private static string? Truncate(string? text, int max)
        => text is null || text.Length <= max ? text : text[..max].TrimEnd() + "…";
}
