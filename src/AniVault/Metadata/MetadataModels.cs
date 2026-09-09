using System.Collections.Generic;
using AniVault.Models;

namespace AniVault.Metadata;

/// <summary>One row in an online search result list. Lightweight — details are fetched separately.</summary>
public sealed record MetadataSearchResult
{
    public required ExternalSource Source { get; init; }

    /// <summary>Provider-specific id, passed back to <see cref="IMetadataProvider.GetDetailsAsync"/>.</summary>
    public required string ExternalId { get; init; }

    public required string Title { get; init; }

    public string? SecondaryTitle { get; init; }

    public int? Year { get; init; }

    public MediaType MediaType { get; init; }

    /// <summary>Remote URL of a small poster/cover, if the provider gives one. Not downloaded here.</summary>
    public string? PosterUrl { get; init; }

    public string? Summary { get; init; }
}

/// <summary>
/// The deliberately minimal metadata AniVault stores for a media item (see spec §19).
/// Personal data (rating, notes, status, tags-as-personal, watched episodes) is never part of this.
/// </summary>
public sealed record MediaMetadata
{
    public required ExternalSource Source { get; init; }

    public required string ExternalId { get; init; }

    public MediaType MediaType { get; init; }

    public required string Title { get; init; }

    public string? OriginalTitle { get; init; }

    public IReadOnlyList<string> AlternativeTitles { get; init; } = [];

    public string? Description { get; init; }

    public System.DateOnly? StartDate { get; init; }

    public System.DateOnly? EndDate { get; init; }

    public int? AirYear { get; init; }

    public AnimeSeason? AirSeason { get; init; }

    public int? EpisodeCount { get; init; }

    public int? RuntimeMinutes { get; init; }

    public string? Country { get; init; }

    public string? OfficialWebsite { get; init; }

    public string? PosterUrl { get; init; }

    public string? BackdropUrl { get; init; }

    public double? ProviderRating { get; init; }

    /// <summary>Genre / tag names suggested by the provider. The user can keep or drop them on import.</summary>
    public IReadOnlyList<string> Genres { get; init; } = [];
}
