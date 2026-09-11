using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AniVault.Data;
using AniVault.Models;
using AniVault.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AniVault.Services.Artwork;

/// <summary>Result of a <see cref="IArtworkService.CompressExistingArtworkAsync"/> pass.</summary>
public sealed record ArtworkCompressionResult(int FilesCompressed, long BytesSaved);

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
    /// Re-encodes every stored poster/backdrop wider than the app's own display cap down to a
    /// smaller JPEG, in place. A one-time maintenance pass for libraries that accumulated
    /// oversized artwork before the cap existed on import (or from a provider that serves very
    /// large originals, e.g. Bangumi) — shrinks disk usage and speeds up opening those items'
    /// detail pages.
    /// </summary>
    Task<ArtworkCompressionResult> CompressExistingArtworkAsync(CancellationToken cancellationToken = default);
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

        return await ImageLoading.SaveThumbnailAsync(poster, thumb, ThumbnailWidth) ? thumb : poster;
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

        var maxWidth = isPoster ? MaxPosterWidth : MaxBackdropWidth;
        var jpegDestination = Path.Combine(assetDir, name + ".jpg");
        var destination = await ImageLoading.CapWidthAsync(sourceImagePath, jpegDestination, maxWidth);
        if (destination is null)
        {
            destination = Path.Combine(assetDir, name + extension);
            File.Copy(sourceImagePath, destination, overwrite: true);
        }

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

    public async Task<ArtworkCompressionResult> CompressExistingArtworkAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var items = await db.Media
            .Where(m => m.PosterPath != null || m.BackdropPath != null)
            .ToListAsync(cancellationToken);

        var filesCompressed = 0;
        long bytesSaved = 0;
        var touched = false;

        foreach (var media in items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var posterSaved = await CompressFileInPlaceAsync(media, isPoster: true, MaxPosterWidth);
            var backdropSaved = await CompressFileInPlaceAsync(media, isPoster: false, MaxBackdropWidth);

            if (posterSaved > 0 || backdropSaved > 0)
            {
                filesCompressed += (posterSaved > 0 ? 1 : 0) + (backdropSaved > 0 ? 1 : 0);
                bytesSaved += posterSaved + backdropSaved;
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
