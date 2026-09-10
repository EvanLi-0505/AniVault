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
/// The single user-defined REST provider. Its behaviour comes entirely from
/// <see cref="CustomProviderConfig"/> (URLs + dotted JSON field paths) that the user edits in
/// Settings. Like every provider it only ever runs from an explicit user action.
/// </summary>
public sealed class CustomMetadataProvider : IMetadataProvider
{
    public const string ApiKeySettingKey = "metadata.apikey.custom";

    private readonly ProviderHttp _http;
    private readonly ICustomProviderStore _store;
    private readonly ISecureSettingsService _secureSettings;

    // Remembers the last search so GetDetailsAsync still works when the user gave no details URL.
    private readonly Dictionary<string, MetadataSearchResult> _lastResults = new();

    public CustomMetadataProvider(
        IHttpClientFactory httpClientFactory,
        ICustomProviderStore store,
        ISecureSettingsService secureSettings)
    {
        _store = store;
        _secureSettings = secureSettings;
        _http = new ProviderHttp(httpClientFactory, "Custom provider");
    }

    public ExternalSource Source => ExternalSource.Custom;

    public string DisplayName
        => _store.Current.IsConfigured && !string.IsNullOrWhiteSpace(_store.Current.Name)
            ? _store.Current.Name
            : "Custom provider";

    public string Description
        => _store.Current.IsConfigured
            ? $"Your own API. Endpoint: {Host(_store.Current.SearchUrl)}"
            : "Add your own REST API — use \"Edit custom provider\" below.";

    public bool RequiresApiKey => false;

    public bool IsConfigured => _store.Current.IsConfigured;

    public bool SupportsMediaType(MediaType mediaType)
        => !_store.Current.IsConfigured || _store.Current.ResolveMediaType() == mediaType;

    public async Task<IReadOnlyList<MetadataSearchResult>> SearchAsync(
        string query, MediaType? preferredMediaType, CancellationToken cancellationToken)
    {
        var config = RequireConfig();
        var url = config.SearchUrl.Replace("{query}", Uri.EscapeDataString(query), StringComparison.Ordinal);

        using var doc = await SendAsync(url, config, cancellationToken);

        var results = new List<MetadataSearchResult>();
        foreach (var item in JsonPath.SelectArray(doc.RootElement, config.ResultsPath))
        {
            var id = JsonPath.SelectString(item, config.IdField);
            var title = JsonPath.SelectString(item, config.TitleField);
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            var result = new MetadataSearchResult
            {
                Source = Source,
                ExternalId = id,
                MediaType = config.ResolveMediaType(),
                Title = title,
                SecondaryTitle = JsonPath.SelectString(item, config.SecondaryTitleField),
                Year = ReadYear(item, config.YearField),
                PosterUrl = JsonPath.SelectString(item, config.PosterField),
                Summary = Truncate(MetadataParsing.CleanText(JsonPath.SelectString(item, config.SummaryField)), 200),
            };

            results.Add(result);
            _lastResults[id] = result;
        }

        return results;
    }

    public async Task<MediaMetadata?> GetDetailsAsync(string externalId, CancellationToken cancellationToken)
    {
        var config = RequireConfig();
        _lastResults.TryGetValue(externalId, out var cached);

        // No details endpoint configured: reuse what the search row already gave us.
        if (string.IsNullOrWhiteSpace(config.DetailsUrl))
        {
            return cached is null
                ? null
                : new MediaMetadata
                {
                    Source = Source,
                    ExternalId = externalId,
                    MediaType = cached.MediaType,
                    Title = cached.Title,
                    OriginalTitle = cached.SecondaryTitle,
                    Description = cached.Summary,
                    AirYear = cached.Year,
                    PosterUrl = cached.PosterUrl,
                };
        }

        var url = config.DetailsUrl.Replace("{id}", Uri.EscapeDataString(externalId), StringComparison.Ordinal);
        using var doc = await SendAsync(url, config, cancellationToken);
        var root = doc.RootElement;

        var start = MetadataParsing.ParseDate(JsonPath.SelectString(root, config.YearField));

        var genres = new List<string>();
        if (!string.IsNullOrWhiteSpace(config.GenresPath))
        {
            foreach (var g in JsonPath.SelectArray(root, config.GenresPath))
            {
                var name = string.IsNullOrWhiteSpace(config.GenreNameField)
                    ? (g.ValueKind == JsonValueKind.String ? g.GetString() : null)
                    : JsonPath.SelectString(g, config.GenreNameField);
                if (!string.IsNullOrWhiteSpace(name))
                {
                    genres.Add(name);
                }
            }
        }

        var mediaType = config.ResolveMediaType();
        return new MediaMetadata
        {
            Source = Source,
            ExternalId = externalId,
            MediaType = mediaType,
            Title = JsonPath.SelectString(root, config.TitleField) ?? cached?.Title ?? "(untitled)",
            OriginalTitle = JsonPath.SelectString(root, config.SecondaryTitleField) ?? cached?.SecondaryTitle,
            Description = MetadataParsing.CleanText(JsonPath.SelectString(root, config.DescriptionField)) ?? cached?.Summary,
            StartDate = start,
            AirYear = ReadYear(root, config.YearField) ?? cached?.Year,
            AirSeason = mediaType == MediaType.Anime ? MetadataParsing.YearAndSeason(start).Season : null,
            EpisodeCount = JsonPath.SelectInt(root, config.EpisodeCountField),
            RuntimeMinutes = JsonPath.SelectInt(root, config.RuntimeField),
            PosterUrl = JsonPath.SelectString(root, config.PosterField) ?? cached?.PosterUrl,
            ProviderRating = JsonPath.SelectDouble(root, config.RatingField) is > 0 and var r ? r : null,
            Genres = genres.Distinct(StringComparer.OrdinalIgnoreCase).Take(12).ToList(),
        };
    }

    private CustomProviderConfig RequireConfig()
    {
        var config = _store.Current;
        if (!config.IsConfigured)
        {
            throw new MetadataProviderException(
                "The custom provider is not set up yet. Open Settings → Metadata → \"Edit custom provider\" first.");
        }

        return config;
    }

    private async Task<JsonDocument> SendAsync(string url, CustomProviderConfig config, CancellationToken cancellationToken)
    {
        var request = _http.NewRequest(HttpMethod.Get, url);

        if (!string.IsNullOrWhiteSpace(config.ApiKeyHeader))
        {
            var key = await _secureSettings.GetAsync(ApiKeySettingKey, cancellationToken);
            if (!string.IsNullOrWhiteSpace(key))
            {
                request.Headers.TryAddWithoutValidation(config.ApiKeyHeader, (config.ApiKeyPrefix ?? string.Empty) + key);
            }
        }

        return await _http.SendAsync(request, cancellationToken);
    }

    private static int? ReadYear(JsonElement element, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        return JsonPath.SelectInt(element, path) is { } n and >= 1900 and <= 2999
            ? n
            : JsonPath.YearFrom(JsonPath.SelectString(element, path));
    }

    private static string Host(string url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : url;

    private static string? Truncate(string? text, int max)
        => text is null || text.Length <= max ? text : text[..max].TrimEnd() + "…";
}
