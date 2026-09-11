using System;
using System.IO;
using System.Linq;
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
    public async Task SetPoster_Keeps_The_Original_Resolution_Even_When_Oversized()
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

        // Downloaded/imported artwork is never auto-shrunk — only the explicit Compress feature
        // (with the user picking which items) may downscale a stored file.
        Assert.Equal($"Anime/{id}/poster.jpg", saved.PosterPath);
        Assert.Equal(2400, ReadPixelWidth(absolute));
    }

    [Fact]
    public async Task FindOversizedArtwork_Finds_A_Poster_Wider_Than_The_Display_Cap()
    {
        using var lib = new TestLibrary();
        var service = new ArtworkService(lib, lib.Paths, NullLogger<ArtworkService>.Instance);
        var id = await SeedOversizedPosterAsync(lib, "Legacy Big Poster");

        var found = await service.FindOversizedArtworkAsync();

        var match = Assert.Single(found, i => i.MediaId == id);
        Assert.True(match.IsPoster);
        Assert.Equal(2400, match.PixelWidth);
        Assert.True(match.FileSizeBytes > 0);
    }

    [Fact]
    public async Task CompressArtwork_Only_Shrinks_The_Items_The_Caller_Selected()
    {
        using var lib = new TestLibrary();
        var service = new ArtworkService(lib, lib.Paths, NullLogger<ArtworkService>.Instance);
        var selectedId = await SeedOversizedPosterAsync(lib, "Selected");
        var untouchedId = await SeedOversizedPosterAsync(lib, "Left Alone");

        var found = await service.FindOversizedArtworkAsync();
        var toCompress = found.Where(i => i.MediaId == selectedId).ToList();
        Assert.Single(toCompress);

        var result = await service.CompressArtworkAsync(toCompress);

        Assert.Equal(1, result.FilesCompressed);
        Assert.True(result.BytesSaved > 0);

        await using var readDb = lib.CreateDbContext();
        var compressed = await readDb.Media.SingleAsync(m => m.Id == selectedId);
        Assert.True(ReadPixelWidth(lib.Paths.ToAbsolutePath(compressed.PosterPath!)) <= 900);

        var untouched = await readDb.Media.SingleAsync(m => m.Id == untouchedId);
        Assert.Equal(2400, ReadPixelWidth(lib.Paths.ToAbsolutePath(untouched.PosterPath!)));
    }

    private async Task<int> SeedOversizedPosterAsync(TestLibrary lib, string title)
    {
        await using var db = lib.CreateDbContext();
        var media = new Media { MediaType = MediaType.Anime, Title = title };
        db.Media.Add(media);
        await db.SaveChangesAsync();

        var assetDir = lib.Paths.GetMediaAssetDirectory(MediaType.Anime, media.Id);
        Directory.CreateDirectory(assetDir);
        var absolute = Path.Combine(assetDir, "poster.jpg");
        var oversized = WriteOversizedJpeg(2400, 3200);
        File.Copy(oversized, absolute, overwrite: true);
        File.Delete(oversized);

        media.PosterPath = lib.Paths.ToRelativePath(absolute);
        await db.SaveChangesAsync();
        return media.Id;
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
    public async Task FindOversizedArtwork_Returns_Empty_When_Everything_Is_Already_Small()
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

        var found = await service.FindOversizedArtworkAsync();

        Assert.DoesNotContain(found, i => i.MediaId == id);
    }
}
