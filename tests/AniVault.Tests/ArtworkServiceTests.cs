using System;
using System.IO;
using System.Threading.Tasks;
using AniVault.Models;
using AniVault.Services.Artwork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AniVault.Tests;

public class ArtworkServiceTests
{
    // A minimal valid 1x1 PNG.
    private static readonly byte[] OnePixelPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private static string WriteTempImage(string extension)
    {
        var path = Path.Combine(Path.GetTempPath(), $"art-{Guid.NewGuid():N}{extension}");
        File.WriteAllBytes(path, OnePixelPng);
        return path;
    }

    [Fact]
    public async Task SetPoster_Copies_File_Sets_Relative_Path_And_Builds_Thumbnail()
    {
        using var lib = new TestLibrary();
        var service = new ArtworkService(lib, lib.Paths, NullLogger<ArtworkService>.Instance);

        int id;
        await using (var db = lib.CreateDbContext())
        {
            var media = new Media { MediaType = MediaType.Anime, Title = "Art" };
            db.Media.Add(media);
            await db.SaveChangesAsync();
            id = media.Id;
        }

        var source = WriteTempImage(".png");
        try
        {
            await service.SetPosterAsync(id, source);
        }
        finally
        {
            File.Delete(source);
        }

        await using (var db = lib.CreateDbContext())
        {
            var media = await db.Media.SingleAsync(m => m.Id == id);
            Assert.Equal($"Anime/{id}/poster.png", media.PosterPath);
            Assert.True(File.Exists(lib.Paths.ToAbsolutePath(media.PosterPath!)));

            var thumb = await service.GetPosterThumbnailAsync(media);
            Assert.NotNull(thumb);
            Assert.True(File.Exists(thumb!));
            Assert.EndsWith(".jpg", thumb);
        }
    }

    [Fact]
    public async Task ClearPoster_Removes_Files_And_Path()
    {
        using var lib = new TestLibrary();
        var service = new ArtworkService(lib, lib.Paths, NullLogger<ArtworkService>.Instance);

        int id;
        await using (var db = lib.CreateDbContext())
        {
            var media = new Media { MediaType = MediaType.Movie, Title = "M" };
            db.Media.Add(media);
            await db.SaveChangesAsync();
            id = media.Id;
        }

        var source = WriteTempImage(".png");
        try
        {
            await service.SetPosterAsync(id, source);
        }
        finally
        {
            File.Delete(source);
        }

        string posterAbsolute;
        await using (var db = lib.CreateDbContext())
        {
            posterAbsolute = lib.Paths.ToAbsolutePath((await db.Media.SingleAsync(m => m.Id == id)).PosterPath!);
        }

        await service.ClearPosterAsync(id);

        Assert.False(File.Exists(posterAbsolute));
        await using (var db = lib.CreateDbContext())
        {
            Assert.Null((await db.Media.SingleAsync(m => m.Id == id)).PosterPath);
        }
    }

    [Fact]
    public async Task DeleteAllArtwork_Removes_The_Asset_Folder()
    {
        using var lib = new TestLibrary();
        var service = new ArtworkService(lib, lib.Paths, NullLogger<ArtworkService>.Instance);

        int id;
        await using (var db = lib.CreateDbContext())
        {
            var media = new Media { MediaType = MediaType.Anime, Title = "Gone" };
            db.Media.Add(media);
            await db.SaveChangesAsync();
            id = media.Id;
        }

        var source = WriteTempImage(".png");
        try
        {
            await service.SetPosterAsync(id, source);
            await service.SetBackdropAsync(id, source);
        }
        finally
        {
            File.Delete(source);
        }

        var assetDir = lib.Paths.GetMediaAssetDirectory(MediaType.Anime, id);
        Assert.True(Directory.Exists(assetDir));

        await service.DeleteAllArtworkAsync(id, MediaType.Anime);

        Assert.False(Directory.Exists(assetDir));
    }

    [Fact]
    public async Task SetPoster_Rejects_Unsupported_Extension()
    {
        using var lib = new TestLibrary();
        var service = new ArtworkService(lib, lib.Paths, NullLogger<ArtworkService>.Instance);

        int id;
        await using (var db = lib.CreateDbContext())
        {
            var media = new Media { MediaType = MediaType.Anime, Title = "X" };
            db.Media.Add(media);
            await db.SaveChangesAsync();
            id = media.Id;
        }

        var source = WriteTempImage(".gif");
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.SetPosterAsync(id, source));
        }
        finally
        {
            File.Delete(source);
        }
    }
}
