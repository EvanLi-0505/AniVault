using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AniVault.Models;
using AniVault.Services;

namespace AniVault.Metadata;

/// <summary>
/// User-supplied description of one custom REST metadata source. Stored as JSON in the settings
/// table (not encrypted — it holds no secret; the optional API key is stored separately via
/// <see cref="ISecureSettingsService"/>). Field paths are dotted, e.g. <c>data.results[0].title</c>.
/// </summary>
public sealed class CustomProviderConfig
{
    public string Name { get; set; } = string.Empty;

    /// <summary>GET URL for a search. Must contain the literal token <c>{query}</c>.</summary>
    public string SearchUrl { get; set; } = string.Empty;

    /// <summary>Optional GET URL for one item's full details. Contains <c>{id}</c>.</summary>
    public string? DetailsUrl { get; set; }

    /// <summary>Optional request header carrying the API key, e.g. <c>Authorization</c>.</summary>
    public string? ApiKeyHeader { get; set; }

    /// <summary>Text placed before the key in that header, e.g. <c>Bearer </c>.</summary>
    public string? ApiKeyPrefix { get; set; }

    /// <summary>Which library the results go to: <c>Anime</c>, <c>Movie</c> or <c>TvSeries</c>.</summary>
    public string MediaType { get; set; } = nameof(Models.MediaType.Anime);

    /// <summary>Path to the results array in the search response ("" = the response is the array).</summary>
    public string ResultsPath { get; set; } = string.Empty;

    // Paths relative to one result item:
    public string IdField { get; set; } = "id";
    public string TitleField { get; set; } = "title";
    public string? SecondaryTitleField { get; set; }
    public string? YearField { get; set; }
    public string? PosterField { get; set; }
    public string? SummaryField { get; set; }

    // Paths relative to the details response (falls back to the search item when no DetailsUrl):
    public string? DescriptionField { get; set; }
    public string? EpisodeCountField { get; set; }
    public string? RuntimeField { get; set; }
    public string? RatingField { get; set; }

    /// <summary>Path to a genres array. Each entry is either a string or an object + <see cref="GenreNameField"/>.</summary>
    public string? GenresPath { get; set; }
    public string? GenreNameField { get; set; }

    public MediaType ResolveMediaType()
        => Enum.TryParse<MediaType>(MediaType, ignoreCase: true, out var t) ? t : Models.MediaType.Anime;

    public bool IsConfigured
        => !string.IsNullOrWhiteSpace(Name)
           && !string.IsNullOrWhiteSpace(SearchUrl)
           && SearchUrl.Contains("{query}", StringComparison.Ordinal)
           && !string.IsNullOrWhiteSpace(TitleField)
           && !string.IsNullOrWhiteSpace(IdField);

    public CustomProviderConfig Clone() => (CustomProviderConfig)MemberwiseClone();
}

/// <summary>Loads / saves the single <see cref="CustomProviderConfig"/> and keeps it in memory.</summary>
public interface ICustomProviderStore
{
    /// <summary>The current config. Never null; check <see cref="CustomProviderConfig.IsConfigured"/>.</summary>
    CustomProviderConfig Current { get; }

    Task LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(CustomProviderConfig config, CancellationToken cancellationToken = default);
}

public sealed class CustomProviderStore : ICustomProviderStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly ISettingsService _settings;

    public CustomProviderStore(ISettingsService settings)
    {
        _settings = settings;
        Current = new CustomProviderConfig();
    }

    public CustomProviderConfig Current { get; private set; }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var raw = await _settings.GetAsync(SettingKeys.CustomProvider, cancellationToken);
        if (string.IsNullOrWhiteSpace(raw))
        {
            Current = new CustomProviderConfig();
            return;
        }

        try
        {
            Current = JsonSerializer.Deserialize<CustomProviderConfig>(raw, JsonOptions) ?? new CustomProviderConfig();
        }
        catch (JsonException)
        {
            Current = new CustomProviderConfig();
        }
    }

    public async Task SaveAsync(CustomProviderConfig config, CancellationToken cancellationToken = default)
    {
        Current = config.Clone();
        await _settings.SetAsync(SettingKeys.CustomProvider, JsonSerializer.Serialize(Current, JsonOptions), cancellationToken);
    }
}
