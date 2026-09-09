using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AniVault.Metadata;
using AniVault.Models;
using AniVault.Services;
using AniVault.Services.Artwork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AniVault.Tests;

public class MetadataImporterTests
{
    private sealed class NoDownloadService : IImageDownloadService
    {
        public Task<string?> DownloadToTempAsync(string url, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);
    }

    private static MetadataImporter CreateImporter(TestLibrary lib) => new(
        lib,
        new TagService(lib),
        new ArtworkService(lib, lib.Paths, NullLogger<ArtworkService>.Instance),
        new NoDownloadService(),
        NullLogger<MetadataImporter>.Instance);

    private static MediaMetadata SampleMetadata(string externalId = "154587") => new()
    {
        Source = ExternalSource.AniList,
        ExternalId = externalId,
        MediaType = MediaType.Anime,
        Title = "Frieren",
        OriginalTitle = "葬送のフリーレン",
        Description = "A fantasy adventure.",
        AirYear = 2023,
        AirSeason = AnimeSeason.Fall,
        EpisodeCount = 28,
        ProviderRating = 9.4,
        Genres = new[] { "Fantasy", "Adventure" },
    };

    [Fact]
    public async Task Import_Creates_Media_With_External_Id_Tags_And_Planned_Status()
    {
        using var lib = new TestLibrary();
        var importer = CreateImporter(lib);

        var id = await importer.ImportAsync(SampleMetadata(), new MetadataImportOptions(ImportGenresAsTags: true));

        await using var db = lib.CreateDbContext();
        var media = await db.Media
            .Include(m => m.ExternalIds)
            .Include(m => m.MediaTags).ThenInclude(mt => mt.Tag)
            .SingleAsync(m => m.Id == id);

        Assert.Equal("Frieren", media.Title);
        Assert.Equal(WatchStatus.Planned, media.Status);
        Assert.Null(media.MyRating);
        Assert.Equal(9.4, media.ProviderRating);
        Assert.Equal(ExternalSource.AniList, media.ExternalIds.Single().Source);
        Assert.Equal(new[] { "Adventure", "Fantasy" },
            media.MediaTags.Select(mt => mt.Tag!.Name).OrderBy(n => n).ToArray());
    }

    [Fact]
    public async Task CheckDuplicate_Detects_Same_Provider_Id_And_Same_Title()
    {
        using var lib = new TestLibrary();
        var importer = CreateImporter(lib);
        var id = await importer.ImportAsync(SampleMetadata(), new MetadataImportOptions());

        var exact = await importer.CheckDuplicateAsync(SampleMetadata());
        Assert.True(exact.IsPossibleDuplicate);
        Assert.Equal(id, exact.ExistingMediaId);

        var byTitle = await importer.CheckDuplicateAsync(SampleMetadata(externalId: "999999"));
        Assert.True(byTitle.IsPossibleDuplicate);
        Assert.Equal(id, byTitle.ExistingMediaId);

        var different = await importer.CheckDuplicateAsync(SampleMetadata(externalId: "42") with { Title = "Something Else" });
        Assert.False(different.IsPossibleDuplicate);
    }

    [Fact]
    public async Task Refresh_Updates_Provider_Fields_But_Keeps_Personal_Data()
    {
        using var lib = new TestLibrary();
        var mediaService = new MediaService(lib);
        var importer = CreateImporter(lib);

        var media = await mediaService.CreateAsync(new Media
        {
            MediaType = MediaType.Anime,
            Title = "Old title",
            Status = WatchStatus.Completed,
            MyRating = 8.0,
            IsFavorite = true,
            Notes = "loved it",
        });
        await new TagService(lib).SetMediaTagsAsync(media.Id, new[] { "MyTag" });

        await importer.RefreshAsync(media.Id, SampleMetadata() with { Title = "New title", Description = "Updated." });

        var refreshed = await mediaService.GetByIdAsync(media.Id);
        Assert.NotNull(refreshed);
        Assert.Equal("New title", refreshed!.Title);          // provider field updated
        Assert.Equal("Updated.", refreshed.Description);
        Assert.Equal(28, refreshed.EpisodeCount);
        Assert.Equal(WatchStatus.Completed, refreshed.Status); // personal data untouched
        Assert.Equal(8.0, refreshed.MyRating);
        Assert.True(refreshed.IsFavorite);
        Assert.Equal("loved it", refreshed.Notes);
        Assert.Equal("MyTag", refreshed.MediaTags.Single().Tag!.Name);
    }
}
