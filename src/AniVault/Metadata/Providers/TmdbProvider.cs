using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AniVault.Models;
using AniVault.Services;

namespace AniVault.Metadata.Providers;

/// <summary>
/// The Movie Database (https://www.themoviedb.org) v3 API. Movies and TV series.
/// Requires a free API key, entered by the user in Settings and stored encrypted.
/// </summary>
public sealed class TmdbProvider : IMetadataProvider
{
    private const string Api = "https://api.themoviedb.org/3";
    private const string ImageBase = "https://image.tmdb.org/t/p/w500";
    private const string BackdropBase = "https://image.tmdb.org/t/p/w1280";

    public const string ApiKeySettingKey = "metadata.apikey.tmdb";

    private readonly ProviderHttp _http;
    private readonly ISecureSettingsService _secureSettings;

    public TmdbProvider(IHttpClientFactory httpClientFactory, ISecureSettingsService secureSettings)
    {
        _http = new ProviderHttp(httpClientFactory, DisplayName);
        _secureSettings = secureSettings;
    }

    public ExternalSource Source => ExternalSource.Tmdb;

    public string DisplayName => "TMDB";

    public string Description => "Movies & TV series. Needs a free API key (themoviedb.org → Settings → API).";

    public bool RequiresApiKey => true;

    public bool SupportsMediaType(MediaType mediaType) => mediaType is MediaType.Movie or MediaType.TvSeries;

    public async Task<IReadOnlyList<MetadataSearchResult>> SearchAsync(
        string query, MediaType? preferredMediaType, CancellationToken cancellationToken)
    {
        var key = await RequireKeyAsync(cancellationToken);
        var escaped = Uri.EscapeDataString(query);

        var endpoint = preferredMediaType switch
        {
            MediaType.Movie => $"{Api}/search/movie?api_key={key}&include_adult=false&query={escaped}",
            MediaType.TvSeries => $"{Api}/search/tv?api_key={key}&include_adult=false&query={escaped}",
            _ => $"{Api}/search/multi?api_key={key}&include_adult=false&query={escaped}",
        };

        using var doc = await _http.GetJsonAsync(endpoint, cancellationToken);

        var results = new List<MetadataSearchResult>();
        if (!doc.RootElement.TryGetProperty("results", out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return results;
        }

        foreach (var item in array.EnumerateArray())
        {
            var kind = item.GetString("media_type")
                       ?? (preferredMediaType == MediaType.TvSeries ? "tv" : "movie");
            if (kind is not ("movie" or "tv"))
            {
                continue;
            }

            var isTv = kind == "tv";
            var date = MetadataParsing.ParseDate(isTv ? item.GetString("first_air_date") : item.GetString("release_date"));

            results.Add(new MetadataSearchResult
            {
                Source = Source,
                ExternalId = $"{kind}:{item.GetInt("id")}",
                MediaType = isTv ? MediaType.TvSeries : MediaType.Movie,
                Title = (isTv ? item.GetString("name") : item.GetString("title")) ?? "(untitled)",
                SecondaryTitle = isTv ? item.GetString("original_name") : item.GetString("original_title"),
                Year = date?.Year,
                PosterUrl = item.GetString("poster_path") is { } p ? ImageBase + p : null,
                Summary = Truncate(item.GetString("overview"), 200),
            });
        }

        return results;
    }

    public async Task<MediaMetadata?> GetDetailsAsync(string externalId, CancellationToken cancellationToken)
    {
        var key = await RequireKeyAsync(cancellationToken);

        var parts = externalId.Split(':', 2);
        if (parts.Length != 2 || !int.TryParse(parts[1], out var id))
        {
            return null;
        }

        var isTv = parts[0] == "tv";
        using var doc = await _http.GetJsonAsync($"{Api}/{(isTv ? "tv" : "movie")}/{id}?api_key={key}", cancellationToken);
        var m = doc.RootElement;
        if (m.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var start = MetadataParsing.ParseDate(isTv ? m.GetString("first_air_date") : m.GetString("release_date"));
        var end = isTv ? MetadataParsing.ParseDate(m.GetString("last_air_date")) : start;

        var genres = m.TryGetProperty("genres", out var g) && g.ValueKind == JsonValueKind.Array
            ? g.EnumerateArray().Select(x => x.GetString("name")).Where(n => !string.IsNullOrWhiteSpace(n)).ToList()!
            : new List<string>();

        string? country = null;
        if (m.TryGetProperty("production_countries", out var pc) && pc.ValueKind == JsonValueKind.Array)
        {
            country = pc.EnumerateArray().FirstOrDefault().GetString("name");
        }

        int? runtime = isTv
            ? (m.TryGetProperty("episode_run_time", out var ert) && ert.ValueKind == JsonValueKind.Array
                ? ert.EnumerateArray().Select(e => e.TryGetInt32(out var v) ? v : (int?)null).FirstOrDefault(v => v is > 0)
                : null)
            : m.GetInt("runtime");

        return new MediaMetadata
        {
            Source = Source,
            ExternalId = externalId,
            MediaType = isTv ? MediaType.TvSeries : MediaType.Movie,
            Title = (isTv ? m.GetString("name") : m.GetString("title")) ?? "(untitled)",
            OriginalTitle = isTv ? m.GetString("original_name") : m.GetString("original_title"),
            Description = m.GetString("overview"),
            StartDate = start,
            EndDate = end,
            AirYear = start?.Year,
            EpisodeCount = isTv ? m.GetInt("number_of_episodes") : null,
            RuntimeMinutes = runtime,
            Country = country,
            OfficialWebsite = string.IsNullOrWhiteSpace(m.GetString("homepage")) ? null : m.GetString("homepage"),
            PosterUrl = m.GetString("poster_path") is { } p ? ImageBase + p : null,
            BackdropUrl = m.GetString("backdrop_path") is { } b ? BackdropBase + b : null,
            ProviderRating = m.GetDouble("vote_average") is > 0 and var v ? v : null,
            Genres = genres,
        };
    }

    private async Task<string> RequireKeyAsync(CancellationToken cancellationToken)
    {
        var key = await _secureSettings.GetAsync(ApiKeySettingKey, cancellationToken);
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new MetadataProviderException("Add your TMDB API key in Settings before searching TMDB.");
        }

        return key;
    }

    private static string? Truncate(string? text, int max)
        => string.IsNullOrWhiteSpace(text) ? null
           : text.Length <= max ? text
           : text[..max].TrimEnd() + "…";
}
