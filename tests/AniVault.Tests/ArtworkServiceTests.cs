using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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

    /// <summary>A solid-color JPEG at an arbitrary size — big enough to exercise the width cap.</summary>
    private static string WriteOversizedJpeg(int width, int height)
    {
        var pixels = new byte[width * height * 4];
        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);

        var path = Path.Combine(Path.GetTempPath(), $"art-big-{Guid.NewGuid():N}.jpg");
        var encoder = new JpegBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(path))
        {
            encoder.Save(stream);
        }

        return path;
    }

    private static int ReadPixelWidth(string path)
    {
        using var stream = File.OpenRead(path);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
        return decoder.Frames[0].PixelWidth;
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

    [Fact]
    public async Task SetPoster_Downscales_An_Oversized_Source_Image()
    {
        using var lib = new TestLibrary();
        var service = new ArtworkService(lib, lib.Paths, NullLogger<ArtworkService>.Instance);

        int id;
        await using (var db = lib.CreateDbContext())
        {
            var media = new Media { MediaType = MediaType.Anime, Title = "Big Poster" };
            db.Media.Add(media);
            await db.SaveChangesAsync();
            id = media.Id;
        }

        var source = WriteOversizedJpeg(2400, 3200);
        try
        {
            await service.SetPosterAsync(id, source);
        }
        finally
        {
            File.Delete(source);
        }

        await using var readDb = lib.CreateDbContext();
        var saved = await readDb.Media.SingleAsync(m => m.Id == id);
        var absolute = lib.Paths.ToAbsolutePath(saved.PosterPath!);

        Assert.Equal($"Anime/{id}/poster.jpg", saved.PosterPath);
        Assert.True(ReadPixelWidth(absolute) <= 900);
    }

    [Fact]
    public async Task CompressExistingArtwork_Shrinks_An_Already_Oversized_Poster()
    {
        using var lib = new TestLibrary();
        var service = new ArtworkService(lib, lib.Paths, NullLogger<ArtworkService>.Instance);

        // Simulate a poster that predates the import-time cap (or came from a provider whose
        // "large" size is huge) by writing it straight to the asset folder, bypassing SetPosterAsync.
        int id;
        string relativePath;
        await using (var db = lib.CreateDbContext())
        {
            var media = new Media { MediaType = MediaType.Anime, Title = "Legacy Big Poster" };
            db.Media.Add(media);
            await db.SaveChangesAsync();
            id = media.Id;

            var assetDir = lib.Paths.GetMediaAssetDirectory(MediaType.Anime, id);
            Directory.CreateDirectory(assetDir);
            var absolute = Path.Combine(assetDir, "poster.jpg");
            var oversized = WriteOversizedJpeg(2400, 3200);
            File.Copy(oversized, absolute, overwrite: true);
            File.Delete(oversized);

            media.PosterPath = lib.Paths.ToRelativePath(absolute);
            relativePath = media.PosterPath;
            await db.SaveChangesAsync();
        }

        var before = new FileInfo(lib.Paths.ToAbsolutePath(relativePath)).Length;

        var result = await service.CompressExistingArtworkAsync();

        Assert.Equal(1, result.FilesCompressed);
        Assert.True(result.BytesSaved > 0);

        await using var readDb = lib.CreateDbContext();
        var saved = await readDb.Media.SingleAsync(m => m.Id == id);
        var absoluteAfter = lib.Paths.ToAbsolutePath(saved.PosterPath!);

        Assert.True(ReadPixelWidth(absoluteAfter) <= 900);
        Assert.True(new FileInfo(absoluteAfter).Length < before);
    }

    [Fact]
    public async Task RelocateArtwork_Moves_Files_And_Updates_Stored_Paths()
    {
        using var lib = new TestLibrary();
        var service = new ArtworkService(lib, lib.Paths, NullLogger<ArtworkService>.Instance);

        int id;
        await using (var db = lib.CreateDbContext())
        {
            var media = new Media { MediaType = MediaType.TvSeries, Title = "Misfiled Movie" };
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

        var oldAssetDir = lib.Paths.GetMediaAssetDirectory(MediaType.TvSeries, id);
        Assert.True(Directory.Exists(oldAssetDir));

        await service.RelocateArtworkAsync(id, MediaType.TvSeries, MediaType.Movie);

        Assert.False(Directory.Exists(oldAssetDir));
        var newAssetDir = lib.Paths.GetMediaAssetDirectory(MediaType.Movie, id);
        Assert.True(Directory.Exists(newAssetDir));

        await using var readDb = lib.CreateDbContext();
        var saved = await readDb.Media.SingleAsync(m => m.Id == id);
        Assert.Equal($"Movies/{id}/poster.png", saved.PosterPath);
        Assert.Equal($"Movies/{id}/backdrop.png", saved.BackdropPath);
        Assert.True(File.Exists(lib.Paths.ToAbsolutePath(saved.PosterPath!)));
        Assert.True(File.Exists(lib.Paths.ToAbsolutePath(saved.BackdropPath!)));
    }

    [Fact]
    public async Task CompressExistingArtwork_Does_Nothing_When_Everything_Is_Already_Small()
    {
        using var lib = new TestLibrary();
        var service = new ArtworkService(lib, lib.Paths, NullLogger<ArtworkService>.Instance);

        int id;
        await using (var db = lib.CreateDbContext())
        {
            var media = new Media { MediaType = MediaType.Anime, Title = "Small Poster" };
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

        var result = await service.CompressExistingArtworkAsync();

        Assert.Equal(0, result.FilesCompressed);
        Assert.Equal(0, result.BytesSaved);
    }
}
