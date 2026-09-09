using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AniVault.Data;
using AniVault.Models;
using AniVault.Services;
using AniVault.Services.Artwork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AniVault.Metadata;

/// <summary>Outcome of checking whether an online result is already in the library.</summary>
public sealed record DuplicateCheck(bool IsPossibleDuplicate, int? ExistingMediaId, string? Reason);

/// <summary>What the user chose to bring in from an online result.</summary>
public sealed record MetadataImportOptions(
    bool ImportGenresAsTags = true,
    bool DownloadPoster = false,
    bool DownloadBackdrop = false);

/// <summary>
/// Turns a <see cref="MediaMetadata"/> into a new local <see cref="Media"/> record.
/// Only provider-owned fields are written; the new item starts as "Planned" with no personal data.
/// Nothing is added to the library without an explicit call to <see cref="ImportAsync"/>.
/// </summary>
public interface IMetadataImporter
{
    Task<DuplicateCheck> CheckDuplicateAsync(MediaMetadata metadata, CancellationToken cancellationToken = default);

    /// <summary>Creates the media item and returns its new id.</summary>
    Task<int> ImportAsync(MediaMetadata metadata, MetadataImportOptions options, CancellationToken cancellationToken = default);

    /// <summary>Re-applies provider-owned fields to an existing item, preserving all personal data (spec §60).</summary>
    Task RefreshAsync(int mediaId, MediaMetadata metadata, CancellationToken cancellationToken = default);
}

public sealed class MetadataImporter : IMetadataImporter
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly ITagService _tagService;
    private readonly IArtworkService _artworkService;
    private readonly IImageDownloadService _imageDownloadService;
    private readonly ILogger<MetadataImporter> _logger;

    public MetadataImporter(
        IDbContextFactory<AppDbContext> contextFactory,
        ITagService tagService,
        IArtworkService artworkService,
        IImageDownloadService imageDownloadService,
        ILogger<MetadataImporter> logger)
    {
        _contextFactory = contextFactory;
        _tagService = tagService;
        _artworkService = artworkService;
        _imageDownloadService = imageDownloadService;
        _logger = logger;
    }

    public async Task<DuplicateCheck> CheckDuplicateAsync(MediaMetadata metadata, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var byExternalId = await db.MediaExternalIds
            .Where(x => x.Source == metadata.Source && x.ExternalId == metadata.ExternalId)
            .Select(x => (int?)x.MediaId)
            .FirstOrDefaultAsync(cancellationToken);
        if (byExternalId is { } exactId)
        {
            return new DuplicateCheck(true, exactId, "This exact item was already imported from the same provider.");
        }

        var title = metadata.Title.Trim();
        var byTitle = await db.Media
            .Where(m => m.MediaType == metadata.MediaType && m.Title.ToLower() == title.ToLower())
            .Select(m => (int?)m.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (byTitle is { } fuzzyId)
        {
            return new DuplicateCheck(true, fuzzyId, "An item with the same title and type already exists in your library.");
        }

        return new DuplicateCheck(false, null, null);
    }

    public async Task<int> ImportAsync(MediaMetadata metadata, MetadataImportOptions options, CancellationToken cancellationToken = default)
    {
        int mediaId;
        await using (var db = await _contextFactory.CreateDbContextAsync(cancellationToken))
        {
            var now = DateTime.UtcNow;
            var media = new Media
            {
                MediaType = metadata.MediaType,
                Status = WatchStatus.Planned,
                CreatedAt = now,
                UpdatedAt = now,
                ExternalIds =
                {
                    new MediaExternalId { Source = metadata.Source, ExternalId = metadata.ExternalId },
                },
            };
            ApplyProviderFields(media, metadata);

            db.Media.Add(media);
            await db.SaveChangesAsync(cancellationToken);
            mediaId = media.Id;
        }

        if (options.ImportGenresAsTags && metadata.Genres.Count > 0)
        {
            await _tagService.SetMediaTagsAsync(mediaId, metadata.Genres, cancellationToken);
        }

        if (options.DownloadPoster)
        {
            await DownloadArtworkAsync(metadata.PosterUrl, path => _artworkService.SetPosterAsync(mediaId, path, cancellationToken), cancellationToken);
        }

        if (options.DownloadBackdrop)
        {
            await DownloadArtworkAsync(metadata.BackdropUrl, path => _artworkService.SetBackdropAsync(mediaId, path, cancellationToken), cancellationToken);
        }

        _logger.LogInformation("Imported media {MediaId} from {Source} {ExternalId}.", mediaId, metadata.Source, metadata.ExternalId);
        return mediaId;
    }

    public async Task RefreshAsync(int mediaId, MediaMetadata metadata, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var media = await db.Media
            .Include(m => m.ExternalIds)
            .FirstOrDefaultAsync(m => m.Id == mediaId, cancellationToken);
        if (media is null)
        {
            return;
        }

        // Provider-owned fields only — rating, favorite, liked, status, notes, tags and
        // watched-episode state are personal and must never be overwritten (spec §60/§61).
        ApplyProviderFields(media, metadata);

        if (!media.ExternalIds.Any(x => x.Source == metadata.Source && x.ExternalId == metadata.ExternalId))
        {
            media.ExternalIds.Add(new MediaExternalId { Source = metadata.Source, ExternalId = metadata.ExternalId });
        }

        media.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static void ApplyProviderFields(Media media, MediaMetadata metadata)
    {
        media.Title = metadata.Title.Trim();
        media.OriginalTitle = Nullify(metadata.OriginalTitle);
        media.AlternativeTitles = metadata.AlternativeTitles.Count > 0
            ? string.Join("\n", metadata.AlternativeTitles)
            : media.AlternativeTitles;
        media.Description = Nullify(metadata.Description) ?? media.Description;
        media.StartDate = metadata.StartDate ?? media.StartDate;
        media.EndDate = metadata.EndDate ?? media.EndDate;
        media.AirYear = metadata.AirYear ?? media.AirYear;
        media.AirSeason = metadata.MediaType == MediaType.Anime ? metadata.AirSeason ?? media.AirSeason : media.AirSeason;
        media.EpisodeCount = metadata.EpisodeCount ?? media.EpisodeCount;
        media.RuntimeMinutes = metadata.RuntimeMinutes ?? media.RuntimeMinutes;
        media.Country = Nullify(metadata.Country) ?? media.Country;
        media.OfficialWebsite = Nullify(metadata.OfficialWebsite) ?? media.OfficialWebsite;
        media.ProviderRating = metadata.ProviderRating ?? media.ProviderRating;
    }

    private async Task DownloadArtworkAsync(string? url, Func<string, Task> apply, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        var tempFile = await _imageDownloadService.DownloadToTempAsync(url, cancellationToken);
        if (tempFile is null)
        {
            return;
        }

        try
        {
            await apply(tempFile);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Downloaded artwork but could not apply it.");
        }
        finally
        {
            try
            {
                File.Delete(tempFile);
            }
            catch (IOException)
            {
            }
        }
    }

    private static string? Nullify(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
