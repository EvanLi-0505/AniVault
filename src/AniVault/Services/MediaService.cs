using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AniVault.Data;
using AniVault.Models;
using Microsoft.EntityFrameworkCore;

namespace AniVault.Services;

/// <summary>
/// Create / read / update / delete operations for <see cref="Media"/>, plus the small
/// focused mutations the UI needs (status, favorite, liked, episode watched state).
/// This is the offline core of the application — no network access ever happens here.
/// </summary>
public interface IMediaService
{
    Task<Media?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Every media item with episodes, tags and external ids loaded. Used by export.</summary>
    Task<IReadOnlyList<Media>> GetAllDetailedAsync(CancellationToken cancellationToken = default);

    /// <summary>Newest items with <see cref="Media.ShowOnHome"/> set, for the Home page.</summary>
    Task<IReadOnlyList<Media>> GetRecentlyAddedAsync(int count, CancellationToken cancellationToken = default);

    Task<int> CountAsync(MediaType mediaType, CancellationToken cancellationToken = default);

    Task<Media> CreateAsync(Media media, CancellationToken cancellationToken = default);

    Task UpdateAsync(Media media, CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    Task SetStatusAsync(int mediaId, WatchStatus status, CancellationToken cancellationToken = default);

    Task SetFavoriteAsync(int mediaId, bool isFavorite, CancellationToken cancellationToken = default);

    Task SetLikedAsync(int mediaId, bool isLiked, CancellationToken cancellationToken = default);

    Task SetShowOnHomeAsync(int mediaId, bool showOnHome, CancellationToken cancellationToken = default);

    Task SetRatingAsync(int mediaId, double? rating, CancellationToken cancellationToken = default);

    /// <summary>
    /// Shows or hides the item's episode checklist on the detail page. Deliberately does not bump
    /// <see cref="Media.UpdatedAt"/> — folding a list away is not an edit, and should not reshuffle
    /// a library sorted by "last updated".
    /// </summary>
    Task SetEpisodeListHiddenAsync(int mediaId, bool hidden, CancellationToken cancellationToken = default);

    /// <summary>Marks a single episode watched/unwatched and stamps <see cref="Episode.WatchedAt"/>.</summary>
    Task SetEpisodeWatchedAsync(int episodeId, bool isWatched, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures the media item has exactly episodes 1..<paramref name="count"/>.
    /// Extra episodes above the count are removed; existing watched state is preserved.
    /// </summary>
    Task SyncEpisodeListAsync(int mediaId, int count, CancellationToken cancellationToken = default);
}

public sealed class MediaService : IMediaService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public MediaService(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<Media?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Media
            .AsNoTracking()
            .AsSplitQuery()
            .Include(m => m.Episodes)
            .Include(m => m.MediaTags).ThenInclude(mt => mt.Tag)
            .Include(m => m.ExternalIds)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Media>> GetAllDetailedAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Media
            .AsNoTracking()
            .AsSplitQuery()
            .Include(m => m.Episodes)
            .Include(m => m.MediaTags).ThenInclude(mt => mt.Tag)
            .Include(m => m.ExternalIds)
            .OrderBy(m => m.MediaType)
            .ThenBy(m => m.Title)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Media>> GetRecentlyAddedAsync(int count, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Media
            .AsNoTracking()
            .Where(m => m.ShowOnHome)
            .OrderByDescending(m => m.CreatedAt)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountAsync(MediaType mediaType, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Media.CountAsync(m => m.MediaType == mediaType, cancellationToken);
    }

    public async Task<Media> CreateAsync(Media media, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var now = DateTime.UtcNow;
        media.CreatedAt = now;
        media.UpdatedAt = now;

        db.Media.Add(media);
        await db.SaveChangesAsync(cancellationToken);
        return media;
    }

    /// <summary>
    /// Updates the scalar fields of an existing media item. Navigation collections
    /// (episodes, tags, external ids) are managed by their own services and are left untouched.
    /// </summary>
    public async Task UpdateAsync(Media media, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var tracked = await db.Media.FirstOrDefaultAsync(m => m.Id == media.Id, cancellationToken);
        if (tracked is null)
        {
            return;
        }

        db.Entry(tracked).CurrentValues.SetValues(media);
        tracked.CreatedAt = tracked.CreatedAt == default ? DateTime.UtcNow : tracked.CreatedAt;
        tracked.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var media = await db.Media.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (media is null)
        {
            return;
        }

        // Episodes, tag links and external ids are removed by cascade delete.
        db.Media.Remove(media);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task SetStatusAsync(int mediaId, WatchStatus status, CancellationToken cancellationToken = default)
        => MutateMediaAsync(mediaId, m =>
        {
            m.Status = status;
            if (status == WatchStatus.Completed)
            {
                m.CompletedAt ??= DateTime.UtcNow;
            }
        }, cancellationToken);

    public Task SetFavoriteAsync(int mediaId, bool isFavorite, CancellationToken cancellationToken = default)
        => MutateMediaAsync(mediaId, m => m.IsFavorite = isFavorite, cancellationToken);

    public Task SetLikedAsync(int mediaId, bool isLiked, CancellationToken cancellationToken = default)
        => MutateMediaAsync(mediaId, m => m.IsLiked = isLiked, cancellationToken);

    public Task SetShowOnHomeAsync(int mediaId, bool showOnHome, CancellationToken cancellationToken = default)
        => MutateMediaAsync(mediaId, m => m.ShowOnHome = showOnHome, cancellationToken);

    public Task SetRatingAsync(int mediaId, double? rating, CancellationToken cancellationToken = default)
        => MutateMediaAsync(mediaId, m => m.MyRating = rating is null ? null : Math.Clamp(rating.Value, 0d, 10d), cancellationToken);

    public async Task SetEpisodeListHiddenAsync(int mediaId, bool hidden, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await db.Media
            .Where(m => m.Id == mediaId)
            .ExecuteUpdateAsync(set => set.SetProperty(m => m.EpisodeListHidden, hidden), cancellationToken);
    }

    public async Task SetEpisodeWatchedAsync(int episodeId, bool isWatched, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var episode = await db.Episodes.Include(e => e.Media)
            .FirstOrDefaultAsync(e => e.Id == episodeId, cancellationToken);
        if (episode is null)
        {
            return;
        }

        episode.IsWatched = isWatched;
        episode.WatchedAt = isWatched ? DateTime.UtcNow : null;
        if (episode.Media is not null)
        {
            episode.Media.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SyncEpisodeListAsync(int mediaId, int count, CancellationToken cancellationToken = default)
    {
        count = Math.Clamp(count, 0, 10_000);

        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var media = await db.Media.Include(m => m.Episodes)
            .FirstOrDefaultAsync(m => m.Id == mediaId, cancellationToken);
        if (media is null)
        {
            return;
        }

        var existing = media.Episodes.ToDictionary(e => e.EpisodeNumber);

        for (var number = 1; number <= count; number++)
        {
            if (!existing.ContainsKey(number))
            {
                media.Episodes.Add(new Episode { EpisodeNumber = number });
            }
        }

        foreach (var episode in media.Episodes.Where(e => e.EpisodeNumber > count).ToList())
        {
            media.Episodes.Remove(episode);
        }

        media.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task MutateMediaAsync(int mediaId, Action<Media> mutate, CancellationToken cancellationToken)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var media = await db.Media.FirstOrDefaultAsync(m => m.Id == mediaId, cancellationToken);
        if (media is null)
        {
            return;
        }

        mutate(media);
        media.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }
}
