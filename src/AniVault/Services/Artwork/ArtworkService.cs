using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AniVault.Data;
using AniVault.Models;
using AniVault.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AniVault.Services.Artwork;

/// <summary>Result of a <see cref="IArtworkService.CompressArtworkAsync"/> pass.</summary>
public sealed record ArtworkCompressionResult(int FilesCompressed, long BytesSaved);

/// <summary>
/// One stored poster or backdrop that's wider than the app ever displays and so is a candidate
/// to shrink via <see cref="IArtworkService.CompressArtworkAsync"/>. Nothing about the file
/// changes just because it shows up in a scan — compression only happens for items the user
/// explicitly selects.
/// </summary>
public sealed record OversizedArtworkItem(int MediaId, string Title, bool IsPoster, int PixelWidth, long FileSizeBytes);

/// <summary>
/// Owns everything about local artwork files: importing a poster / backdrop into a media
/// item's asset folder, maintaining a small on-disk thumbnail for grid cards, and cleaning
/// up when artwork or the whole item is removed. All offline.
/// </summary>
public interface IArtworkService
{
    /// <summary>Copies an image into the item's folder as its poster, refreshes the thumbnail, and saves the relative path.</summary>
    Task SetPosterAsync(int mediaId, string sourceImagePath, CancellationToken cancellationToken = default);

    Task SetBackdropAsync(int mediaId, string sourceImagePath, CancellationToken cancellationToken = default);

    Task ClearPosterAsync(int mediaId, CancellationToken cancellationToken = default);

    Task ClearBackdropAsync(int mediaId, CancellationToken cancellationToken = default);

    /// <summary>Removes every artwork file for an item that is being deleted.</summary>
    Task DeleteAllArtworkAsync(int mediaId, MediaType mediaType, CancellationToken cancellationToken = default);

    /// <summary>Absolute path to a small cached poster thumbnail, generating it on demand. Null if there is no poster.</summary>
    Task<string?> GetPosterThumbnailAsync(Media media, CancellationToken cancellationToken = default);

    /// <summary>Absolute path to the full poster image, or null if none / missing.</summary>
    string? GetPosterPath(Media media);

    /// <summary>Absolute path to the full backdrop image, or null if none / missing.</summary>
    string? GetBackdropPath(Media media);

    /// <summary>
    /// Scans the whole library for a stored poster/backdrop wider than the app ever displays.
    /// Read-only — nothing on disk changes just from calling this.
    /// </summary>
    Task<IReadOnlyList<OversizedArtworkItem>> FindOversizedArtworkAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-encodes exactly the given poster/backdrop selections down to a smaller JPEG, in place
    /// (only replaces a file if the result actually comes out smaller). Downloaded/imported
    /// artwork is always kept at its original resolution — this is the only path that ever
    /// shrinks a stored file, and only for items the user explicitly picked from
    /// <see cref="FindOversizedArtworkAsync"/>'s results.
    /// </summary>
    Task<ArtworkCompressionResult> CompressArtworkAsync(
        IReadOnlyList<OversizedArtworkItem> selection, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves an item's artwork folder from <paramref name="oldType"/>'s asset directory to
    /// <paramref name="newType"/>'s and updates its stored poster/backdrop paths to match.
    /// Called after the editor's manual category override changes an existing item's
    /// <see cref="Media.MediaType"/> — without this, the files stay findable (the app reads the
    /// exact stored path, not one derived from the current type) but end up orphaned on delete,
    /// since <see cref="DeleteAllArtworkAsync"/> looks for the folder the *current* type implies.
    /// No-op if the item has no artwork or the two types share a folder.
    /// </summary>
    Task RelocateArtworkAsync(int mediaId, MediaType oldType, MediaType newType, CancellationToken cancellationToken = default);
}

public sealed class ArtworkService : IArtworkService
{
    private const int ThumbnailWidth = 360;

    // Nothing in the app displays a poster wider than ~500px or a backdrop wider than ~1280px
    // (see MediaDetailViewModel.LoadArtworkAsync), so the "full" file on disk never needs to be
    // any larger than this — a provider's original artwork can be several thousand pixels wide.
    private const int MaxPosterWidth = 900;
    private const int MaxBackdropWidth = 1600;

    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp", ".bmp" };

    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly IAppPathService _paths;
    private readonly ILogger<ArtworkService> _logger;

    public ArtworkService(
        IDbContextFactory<AppDbContext> contextFactory,
        IAppPathService paths,
        ILogger<ArtworkService> logger)
    {
        _contextFactory = contextFactory;
        _paths = paths;
        _logger = logger;
    }

    public Task SetPosterAsync(int mediaId, string sourceImagePath, CancellationToken cancellationToken = default)
        => ImportAsync(mediaId, sourceImagePath, isPoster: true, cancellationToken);

    public Task SetBackdropAsync(int mediaId, string sourceImagePath, CancellationToken cancellationToken = default)
        => ImportAsync(mediaId, sourceImagePath, isPoster: false, cancellationToken);

    public async Task ClearPosterAsync(int mediaId, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var media = await db.Media.FirstOrDefaultAsync(m => m.Id == mediaId, cancellationToken);
        if (media?.PosterPath is null)
        {
            return;
        }

        TryDelete(_paths.ToAbsolutePath(media.PosterPath));
        TryDelete(ThumbnailPath(mediaId));
        media.PosterPath = null;
        media.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ClearBackdropAsync(int mediaId, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var media = await db.Media.FirstOrDefaultAsync(m => m.Id == mediaId, cancellationToken);
        if (media?.BackdropPath is null)
        {
            return;
        }

        TryDelete(_paths.ToAbsolutePath(media.BackdropPath));
        media.BackdropPath = null;
        media.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task DeleteAllArtworkAsync(int mediaId, MediaType mediaType, CancellationToken cancellationToken = default)
    {
        try
        {
            var assetDir = _paths.GetMediaAssetDirectory(mediaType, mediaId);
            if (Directory.Exists(assetDir))
            {
                Directory.Delete(assetDir, recursive: true);
            }

            TryDelete(ThumbnailPath(mediaId));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not remove artwork for media {MediaId}.", mediaId);
        }

        return Task.CompletedTask;
    }

    public async Task<string?> GetPosterThumbnailAsync(Media media, CancellationToken cancellationToken = default)
    {
        var poster = GetPosterPath(media);
        if (poster is null)
        {
            return null;
        }

        var thumb = ThumbnailPath(media.Id);
        if (File.Exists(thumb) && File.GetLastWriteTimeUtc(thumb) >= File.GetLastWriteTimeUtc(poster))
        {
            return thumb;
        }

        var built = await ImageLoading.SaveThumbnailAsync(poster, thumb, ThumbnailWidth);
        return built ? thumb : poster;
    }

    public string? GetPosterPath(Media media) => ResolveExisting(media.PosterPath);

    public string? GetBackdropPath(Media media) => ResolveExisting(media.BackdropPath);

    private async Task ImportAsync(int mediaId, string sourceImagePath, bool isPoster, CancellationToken cancellationToken)
    {
        if (!File.Exists(sourceImagePath))
        {
            throw new FileNotFoundException("The selected image no longer exists.", sourceImagePath);
        }

        var extension = Path.GetExtension(sourceImagePath).ToLowerInvariant();
        if (Array.IndexOf(AllowedExtensions, extension) < 0)
        {
            throw new InvalidOperationException("Unsupported image type. Use JPG, PNG, WEBP or BMP.");
        }

        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var media = await db.Media.FirstOrDefaultAsync(m => m.Id == mediaId, cancellationToken)
            ?? throw new InvalidOperationException("Media item not found.");

        var assetDir = _paths.GetMediaAssetDirectory(media.MediaType, media.Id);
        Directory.CreateDirectory(assetDir);

        var name = isPoster ? "poster" : "backdrop";

        // Remove any previous file of this kind (extension may differ).
        foreach (var existing in Directory.EnumerateFiles(assetDir, name + ".*"))
        {
            TryDelete(existing);
        }

        // Always keep the original resolution here — downscaling only ever happens through
        // CompressArtworkAsync, and only for files the user explicitly selects there.
        var destination = Path.Combine(assetDir, name + extension);
        File.Copy(sourceImagePath, destination, overwrite: true);
        var relative = _paths.ToRelativePath(destination);

        if (isPoster)
        {
            media.PosterPath = relative;
            await ImageLoading.SaveThumbnailAsync(destination, ThumbnailPath(media.Id), ThumbnailWidth);
        }
        else
        {
            media.BackdropPath = relative;
        }

        media.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RelocateArtworkAsync(int mediaId, MediaType oldType, MediaType newType, CancellationToken cancellationToken = default)
    {
        if (oldType == newType)
        {
            return;
        }

        var oldDir = _paths.GetMediaAssetDirectory(oldType, mediaId);
        var newDir = _paths.GetMediaAssetDirectory(newType, mediaId);
        if (string.Equals(oldDir, newDir, StringComparison.OrdinalIgnoreCase) || !Directory.Exists(oldDir))
        {
            return;
        }

        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var media = await db.Media.FirstOrDefaultAsync(m => m.Id == mediaId, cancellationToken);
        if (media is null)
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(newDir)!);
        if (Directory.Exists(newDir))
        {
            // Ids are globally unique across every type, so this shouldn't normally happen —
            // fall back to copy+delete instead of letting Directory.Move throw on a stale folder.
            foreach (var file in Directory.EnumerateFiles(oldDir))
            {
                File.Copy(file, Path.Combine(newDir, Path.GetFileName(file)), overwrite: true);
            }

            Directory.Delete(oldDir, recursive: true);
        }
        else
        {
            Directory.Move(oldDir, newDir);
        }

        var poster = Directory.EnumerateFiles(newDir, "poster.*").FirstOrDefault();
        var backdrop = Directory.EnumerateFiles(newDir, "backdrop.*").FirstOrDefault();

        media.PosterPath = poster is null ? null : _paths.ToRelativePath(poster);
        media.BackdropPath = backdrop is null ? null : _paths.ToRelativePath(backdrop);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OversizedArtworkItem>> FindOversizedArtworkAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var items = await db.Media
            .AsNoTracking()
            .Where(m => m.PosterPath != null || m.BackdropPath != null)
            .Select(m => new { m.Id, m.Title, m.PosterPath, m.BackdropPath })
            .ToListAsync(cancellationToken);

        var results = new List<OversizedArtworkItem>();
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (item.PosterPath is { } posterRelative)
            {
                TryAddIfOversized(results, item.Id, item.Title, isPoster: true, posterRelative, MaxPosterWidth);
            }

            if (item.BackdropPath is { } backdropRelative)
            {
                TryAddIfOversized(results, item.Id, item.Title, isPoster: false, backdropRelative, MaxBackdropWidth);
            }
        }

        return results;
    }

    private void TryAddIfOversized(
        List<OversizedArtworkItem> results, int mediaId, string title, bool isPoster, string relativePath, int maxWidth)
    {
        var absolute = _paths.ToAbsolutePath(relativePath);
        if (!File.Exists(absolute))
        {
            return;
        }

        var width = ImageLoading.TryGetPixelWidth(absolute);
        if (width is { } w && w > maxWidth)
        {
            results.Add(new OversizedArtworkItem(mediaId, title, isPoster, w, new FileInfo(absolute).Length));
        }
    }

    public async Task<ArtworkCompressionResult> CompressArtworkAsync(
        IReadOnlyList<OversizedArtworkItem> selection, CancellationToken cancellationToken = default)
    {
        if (selection.Count == 0)
        {
            return new ArtworkCompressionResult(0, 0);
        }

        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var mediaIds = selection.Select(s => s.MediaId).Distinct().ToList();
        var mediaById = await db.Media
            .Where(m => mediaIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, cancellationToken);

        var filesCompressed = 0;
        long bytesSaved = 0;
        var touched = false;

        foreach (var item in selection)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!mediaById.TryGetValue(item.MediaId, out var media))
            {
                continue;
            }

            var maxWidth = item.IsPoster ? MaxPosterWidth : MaxBackdropWidth;
            var saved = await CompressFileInPlaceAsync(media, item.IsPoster, maxWidth);
            if (saved > 0)
            {
                filesCompressed++;
                bytesSaved += saved;
                media.UpdatedAt = DateTime.UtcNow;
                touched = true;
            }
        }

        if (touched)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return new ArtworkCompressionResult(filesCompressed, bytesSaved);
    }

    /// <summary>
    /// Re-encodes one existing poster/backdrop down to <paramref name="maxWidth"/> if it's
    /// currently wider, mutating <paramref name="media"/>'s path field in place (the caller
    /// saves). Returns bytes saved, or 0 if nothing changed (already small enough, decode failed,
    /// or the re-encode somehow didn't come out smaller).
    /// </summary>
    private async Task<long> CompressFileInPlaceAsync(Media media, bool isPoster, int maxWidth)
    {
        var relative = isPoster ? media.PosterPath : media.BackdropPath;
        if (relative is null)
        {
            return 0;
        }

        var absolute = _paths.ToAbsolutePath(relative);
        if (!File.Exists(absolute))
        {
            return 0;
        }

        var before = new FileInfo(absolute).Length;
        var dir = Path.GetDirectoryName(absolute)!;
        var baseName = Path.GetFileNameWithoutExtension(absolute);
        var tempJpeg = Path.Combine(dir, baseName + ".compressing.jpg");

        var written = await ImageLoading.CapWidthAsync(absolute, tempJpeg, maxWidth);
        if (written is null)
        {
            return 0;
        }

        var after = new FileInfo(tempJpeg).Length;
        if (after >= before)
        {
            TryDelete(tempJpeg);
            return 0;
        }

        var finalPath = Path.Combine(dir, baseName + ".jpg");
        TryDelete(absolute);
        File.Move(tempJpeg, finalPath, overwrite: true);

        var relativeFinal = _paths.ToRelativePath(finalPath);
        if (isPoster)
        {
            media.PosterPath = relativeFinal;
            await ImageLoading.SaveThumbnailAsync(finalPath, ThumbnailPath(media.Id), ThumbnailWidth);
        }
        else
        {
            media.BackdropPath = relativeFinal;
        }

        return before - after;
    }

    private string ThumbnailPath(int mediaId)
        => Path.Combine(_paths.CacheDirectory, "thumbnails", $"poster-{mediaId}.jpg");

    private string? ResolveExisting(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || !_paths.IsConfigured)
        {
            return null;
        }

        var absolute = _paths.ToAbsolutePath(relativePath);
        return File.Exists(absolute) ? absolute : null;
    }

    private void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException ex)
        {
            _logger.LogDebug(ex, "Could not delete artwork file {Path}.", path);
        }
    }
}
