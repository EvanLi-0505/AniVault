using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AniVault.Data;
using AniVault.Models;
using Microsoft.EntityFrameworkCore;

namespace AniVault.Services;

/// <summary>
/// Runs combined filter + sort queries over the local library. One place translates a
/// <see cref="MediaFilter"/> to SQL, so the library page, the search page and the sidebar
/// shortcuts (Favorites, a status, a tag…) all share the exact same behaviour.
/// </summary>
public interface IMediaQueryService
{
    Task<IReadOnlyList<Media>> QueryAsync(
        MediaFilter filter,
        MediaSortOption sort,
        CancellationToken cancellationToken = default);

    /// <summary>Distinct broadcast years present in the library, newest first (for the year filter / season browser).</summary>
    Task<IReadOnlyList<int>> GetBroadcastYearsAsync(
        MediaType? mediaType = null,
        CancellationToken cancellationToken = default);

    /// <summary>Anime item counts grouped by broadcast year and season, for the season browser.</summary>
    Task<IReadOnlyList<AnimeSeasonBucket>> GetAnimeSeasonBucketsAsync(CancellationToken cancellationToken = default);
}

/// <summary>How many anime were broadcast in a given year/season (null season = year known, season not set).</summary>
public sealed record AnimeSeasonBucket(int Year, AnimeSeason? Season, int Count);

public sealed class MediaQueryService : IMediaQueryService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public MediaQueryService(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<IReadOnlyList<Media>> QueryAsync(
        MediaFilter filter,
        MediaSortOption sort,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var query = db.Media
            .AsNoTracking()
            .Include(m => m.MediaTags).ThenInclude(mt => mt.Tag)
            .AsQueryable();

        if (filter.MediaType is { } type)
        {
            query = query.Where(m => m.MediaType == type);
        }

        if (!string.IsNullOrWhiteSpace(filter.Text))
        {
            var term = $"%{filter.Text.Trim()}%";
            query = query.Where(m =>
                EF.Functions.Like(m.Title, term)
                || (m.OriginalTitle != null && EF.Functions.Like(m.OriginalTitle, term))
                || (m.AlternativeTitles != null && EF.Functions.Like(m.AlternativeTitles, term))
                || (m.Description != null && EF.Functions.Like(m.Description, term))
                || m.MediaTags.Any(mt => mt.Tag != null && EF.Functions.Like(mt.Tag.Name, term)));
        }

        if (filter.Status is { } status)
        {
            query = query.Where(m => m.Status == status);
        }

        if (filter.Year is { } year)
        {
            query = query.Where(m => m.AirYear == year);
        }

        if (filter.Season is { } season)
        {
            query = query.Where(m => m.AirSeason == season);
        }

        if (filter.Month is { } month)
        {
            query = query.Where(m => m.AirMonth == month);
        }

        if (filter.IsFavorite is { } fav)
        {
            query = query.Where(m => m.IsFavorite == fav);
        }

        if (filter.IsLiked is { } liked)
        {
            query = query.Where(m => m.IsLiked == liked);
        }

        if (filter.MinRating is { } min)
        {
            query = query.Where(m => m.MyRating >= min);
        }

        if (filter.TagIds.Count > 0)
        {
            var tagIds = filter.TagIds.ToList();
            query = filter.MatchAllTags
                ? query.Where(m => m.MediaTags.Count(mt => tagIds.Contains(mt.TagId)) == tagIds.Count)
                : query.Where(m => m.MediaTags.Any(mt => tagIds.Contains(mt.TagId)));
        }

        query = ApplySort(query, sort);

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<int>> GetBroadcastYearsAsync(
        MediaType? mediaType = null,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var query = db.Media.AsNoTracking().Where(m => m.AirYear != null);
        if (mediaType is { } type)
        {
            query = query.Where(m => m.MediaType == type);
        }

        return await query
            .Select(m => m.AirYear!.Value)
            .Distinct()
            .OrderByDescending(y => y)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AnimeSeasonBucket>> GetAnimeSeasonBucketsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var grouped = await db.Media
            .AsNoTracking()
            .Where(m => m.MediaType == MediaType.Anime && m.AirYear != null)
            .GroupBy(m => new { Year = m.AirYear!.Value, m.AirSeason })
            .Select(g => new { g.Key.Year, g.Key.AirSeason, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return grouped
            .Select(x => new AnimeSeasonBucket(x.Year, x.AirSeason, x.Count))
            .OrderByDescending(b => b.Year)
            .ThenBy(b => b.Season)
            .ToList();
    }

    private static IQueryable<Media> ApplySort(IQueryable<Media> query, MediaSortOption sort)
    {
        var desc = sort.Descending;

        return sort.Field switch
        {
            MediaSortField.Title => desc
                ? query.OrderByDescending(m => m.Title)
                : query.OrderBy(m => m.Title),
            MediaSortField.MyRating => desc
                ? query.OrderByDescending(m => m.MyRating).ThenBy(m => m.Title)
                : query.OrderBy(m => m.MyRating).ThenBy(m => m.Title),
            MediaSortField.BroadcastDate => desc
                ? query.OrderByDescending(m => m.AirYear).ThenByDescending(m => m.StartDate)
                : query.OrderBy(m => m.AirYear).ThenBy(m => m.StartDate),
            MediaSortField.AddedDate => desc
                ? query.OrderByDescending(m => m.CreatedAt)
                : query.OrderBy(m => m.CreatedAt),
            MediaSortField.UpdatedDate => desc
                ? query.OrderByDescending(m => m.UpdatedAt)
                : query.OrderBy(m => m.UpdatedAt),
            MediaSortField.CompletedDate => desc
                ? query.OrderByDescending(m => m.CompletedAt).ThenBy(m => m.Title)
                : query.OrderBy(m => m.CompletedAt).ThenBy(m => m.Title),
            MediaSortField.EpisodeCount => desc
                ? query.OrderByDescending(m => m.EpisodeCount).ThenBy(m => m.Title)
                : query.OrderBy(m => m.EpisodeCount).ThenBy(m => m.Title),
            _ => query.OrderByDescending(m => m.UpdatedAt),
        };
    }
}
