using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AniVault.Data;
using AniVault.Models;
using Microsoft.EntityFrameworkCore;

namespace AniVault.Services;

/// <summary>A tag together with how many media items currently use it.</summary>
public sealed record TagUsage(int Id, string Name, int MediaCount);

/// <summary>
/// Tag management: create, rename, delete, and assign tags to media. Case-insensitive
/// de-duplication is enforced through <see cref="Tag.NormalizedName"/>.
/// </summary>
public interface ITagService
{
    Task<IReadOnlyList<TagUsage>> GetAllWithUsageAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns the existing tag with this name, or creates it. Names are trimmed; case-insensitive.</summary>
    Task<Tag> GetOrCreateAsync(string name, CancellationToken cancellationToken = default);

    Task RenameAsync(int tagId, string newName, CancellationToken cancellationToken = default);

    Task DeleteAsync(int tagId, CancellationToken cancellationToken = default);

    /// <summary>Deletes every tag that is not attached to any media. Returns how many were removed.</summary>
    Task<int> DeleteUnusedAsync(CancellationToken cancellationToken = default);

    /// <summary>Replaces a media item's tag set with exactly <paramref name="tagNames"/> (created as needed).</summary>
    Task SetMediaTagsAsync(int mediaId, IEnumerable<string> tagNames, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rewrites every listed tag's <see cref="Tag.SortOrder"/> to match its position in
    /// <paramref name="orderedTagIds"/> (0-based). Drives the Tags page's drag-to-reorder.
    /// </summary>
    Task ReorderAsync(IReadOnlyList<int> orderedTagIds, CancellationToken cancellationToken = default);
}

public sealed class TagService : ITagService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public TagService(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public static string Normalize(string name) => name.Trim().ToLowerInvariant();

    public async Task<IReadOnlyList<TagUsage>> GetAllWithUsageAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Tags
            .AsNoTracking()
            .OrderBy(t => t.SortOrder)
            .ThenBy(t => t.Id)
            .Select(t => new TagUsage(t.Id, t.Name, t.MediaTags.Count))
            .ToListAsync(cancellationToken);
    }

    public async Task<Tag> GetOrCreateAsync(string name, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var tag = await GetOrCreateInContextAsync(db, name, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return tag;
    }

    public async Task RenameAsync(int tagId, string newName, CancellationToken cancellationToken = default)
    {
        var trimmed = newName.Trim();
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("A tag name cannot be empty.", nameof(newName));
        }

        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var tag = await db.Tags.FirstOrDefaultAsync(t => t.Id == tagId, cancellationToken)
            ?? throw new InvalidOperationException("Tag not found.");

        var normalized = Normalize(trimmed);
        if (await db.Tags.AnyAsync(t => t.Id != tagId && t.NormalizedName == normalized, cancellationToken))
        {
            throw new InvalidOperationException($"A tag called \"{trimmed}\" already exists.");
        }

        tag.Name = trimmed;
        tag.NormalizedName = normalized;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int tagId, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var tag = await db.Tags.FirstOrDefaultAsync(t => t.Id == tagId, cancellationToken);
        if (tag is null)
        {
            return;
        }

        // MediaTag links are removed by cascade delete.
        db.Tags.Remove(tag);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> DeleteUnusedAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var unused = await db.Tags.Where(t => t.MediaTags.Count == 0).ToListAsync(cancellationToken);
        db.Tags.RemoveRange(unused);
        await db.SaveChangesAsync(cancellationToken);
        return unused.Count;
    }

    public async Task SetMediaTagsAsync(int mediaId, IEnumerable<string> tagNames, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var media = await db.Media
            .Include(m => m.MediaTags)
            .FirstOrDefaultAsync(m => m.Id == mediaId, cancellationToken);
        if (media is null)
        {
            return;
        }

        var wanted = new Dictionary<string, Tag>();
        foreach (var raw in tagNames)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var key = Normalize(raw);
            if (!wanted.ContainsKey(key))
            {
                wanted[key] = await GetOrCreateInContextAsync(db, raw, cancellationToken);
            }
        }

        await db.SaveChangesAsync(cancellationToken); // ensure new tags have ids

        var wantedIds = wanted.Values.Select(t => t.Id).ToHashSet();

        media.MediaTags.RemoveAll(mt => !wantedIds.Contains(mt.TagId));
        var currentIds = media.MediaTags.Select(mt => mt.TagId).ToHashSet();
        foreach (var id in wantedIds.Where(id => !currentIds.Contains(id)))
        {
            media.MediaTags.Add(new MediaTag { MediaId = media.Id, TagId = id });
        }

        media.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ReorderAsync(IReadOnlyList<int> orderedTagIds, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var tags = await db.Tags
            .Where(t => orderedTagIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, cancellationToken);

        for (var i = 0; i < orderedTagIds.Count; i++)
        {
            if (tags.TryGetValue(orderedTagIds[i], out var tag))
            {
                tag.SortOrder = i;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task<Tag> GetOrCreateInContextAsync(AppDbContext db, string name, CancellationToken cancellationToken)
    {
        var trimmed = name.Trim();
        var normalized = Normalize(trimmed);

        var existing = await db.Tags.FirstOrDefaultAsync(t => t.NormalizedName == normalized, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var created = new Tag { Name = trimmed, NormalizedName = normalized, SortOrder = await NextSortOrderAsync(db, cancellationToken) };
        db.Tags.Add(created);
        return created;
    }

    /// <summary>
    /// One past the highest <see cref="Tag.SortOrder"/> currently in play, so a newly-created
    /// tag lands at the end of the user's arrangement. Looks at both saved rows and tags already
    /// added-but-unsaved in this same context (e.g. mid-way through a multi-tag <see cref="SetMediaTagsAsync"/> call).
    /// </summary>
    private static async Task<int> NextSortOrderAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var maxSaved = await db.Tags.Select(t => (int?)t.SortOrder).MaxAsync(cancellationToken) ?? -1;
        var maxPending = db.ChangeTracker.Entries<Tag>()
            .Where(e => e.State == EntityState.Added)
            .Select(e => (int?)e.Entity.SortOrder)
            .DefaultIfEmpty(null)
            .Max() ?? -1;

        return Math.Max(maxSaved, maxPending) + 1;
    }
}
