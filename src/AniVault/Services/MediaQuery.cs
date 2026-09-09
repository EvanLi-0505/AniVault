using System.Collections.Generic;
using AniVault.Models;

namespace AniVault.Services;

/// <summary>
/// A combinable set of library filters. Every property is optional; a null property means
/// "don't filter on this". Built by the UI (filter panel) and handed to <see cref="IMediaQueryService"/>.
/// </summary>
public sealed record MediaFilter
{
    /// <summary>Restrict to one library. Null = search across Anime, Movies and TV.</summary>
    public MediaType? MediaType { get; init; }

    /// <summary>Free text matched against titles, alternative titles, description and tag names.</summary>
    public string? Text { get; init; }

    public WatchStatus? Status { get; init; }

    public int? Year { get; init; }

    public AnimeSeason? Season { get; init; }

    public bool? IsFavorite { get; init; }

    public bool? IsLiked { get; init; }

    /// <summary>Minimum personal rating (inclusive).</summary>
    public double? MinRating { get; init; }

    /// <summary>Tag ids the item must carry. Combined per <see cref="MatchAllTags"/>.</summary>
    public IReadOnlyList<int> TagIds { get; init; } = [];

    /// <summary>true = item must have every listed tag; false = any one is enough.</summary>
    public bool MatchAllTags { get; init; } = true;

    public bool HasAnyCondition =>
        MediaType is not null
        || !string.IsNullOrWhiteSpace(Text)
        || Status is not null
        || Year is not null
        || Season is not null
        || IsFavorite is not null
        || IsLiked is not null
        || MinRating is not null
        || TagIds.Count > 0;
}

/// <summary>Fields the library can be ordered by.</summary>
public enum MediaSortField
{
    Title,
    MyRating,
    BroadcastDate,
    AddedDate,
    UpdatedDate,
    CompletedDate,
    EpisodeCount,
}

/// <summary>A sort field plus direction.</summary>
public sealed record MediaSortOption(MediaSortField Field, bool Descending)
{
    public static MediaSortOption Default { get; } = new(MediaSortField.UpdatedDate, Descending: true);
}
