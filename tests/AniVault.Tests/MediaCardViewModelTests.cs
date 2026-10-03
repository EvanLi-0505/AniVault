using System;
using System.IO;
using System.Threading.Tasks;
using AniVault.Models;
using AniVault.Services.Artwork;
using AniVault.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;

namespace AniVault.Tests;

public class MediaCardViewModelTests
{
    // A minimal valid 1x1 PNG.
    private static readonly byte[] OnePixelPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    [Fact]
    public async Task A_Card_Without_A_Poster_Never_Shows_The_Skeleton()
    {
        using var lib = new TestLibrary();
        var artwork = new ArtworkService(lib, lib.Paths, NullLogger<ArtworkService>.Instance);
        var card = new MediaCardViewModel(new Media { Id = 1, MediaType = MediaType.Anime, Title = "No art" }, artwork);

        Assert.False(card.IsPosterLoading);

        await card.LoadArtworkAsync();

        Assert.False(card.IsPosterLoading);
        Assert.Null(card.Poster);
    }

    [Fact]
    public async Task A_Card_With_A_Poster_Is_Loading_Until_The_Thumbnail_Arrives()
    {
        using var lib = new TestLibrary();
        var artwork = new ArtworkService(lib, lib.Paths, NullLogger<ArtworkService>.Instance);

        Media media;
        await using (var db = lib.CreateDbContext())
        {
            media = new Media { MediaType = MediaType.Anime, Title = "Has art" };
            db.Media.Add(media);
            await db.SaveChangesAsync();
        }

        var source = Path.Combine(Path.GetTempPath(), $"card-art-{Guid.NewGuid():N}.png");
        await File.WriteAllBytesAsync(source, OnePixelPng);
        try
        {
            await artwork.SetPosterAsync(media.Id, source);
        }
        finally
        {
            File.Delete(source);
        }

        await using (var db = lib.CreateDbContext())
        {
            media = (await db.Media.FindAsync(media.Id))!;
        }

        var card = new MediaCardViewModel(media, artwork);
        Assert.True(card.IsPosterLoading);

        await card.LoadArtworkAsync();

        Assert.False(card.IsPosterLoading);
        Assert.NotNull(card.Poster);
    }

    [Fact]
    public async Task A_Poster_File_That_Has_Gone_Missing_Still_Ends_The_Skeleton()
    {
        using var lib = new TestLibrary();
        var artwork = new ArtworkService(lib, lib.Paths, NullLogger<ArtworkService>.Instance);
        var card = new MediaCardViewModel(
            new Media { Id = 7, MediaType = MediaType.Anime, Title = "Dangling", PosterPath = "Anime/7/poster.jpg" }, artwork);
        Assert.True(card.IsPosterLoading);

        await card.LoadArtworkAsync();

        Assert.False(card.IsPosterLoading);
        Assert.Null(card.Poster);
    }
}
