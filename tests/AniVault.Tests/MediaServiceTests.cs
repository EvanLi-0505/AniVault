using System.Linq;
using System.Threading.Tasks;
using AniVault.Models;
using AniVault.Services;

namespace AniVault.Tests;

public class MediaServiceTests
{
    private static async Task<Media> SeedAsync(MediaService service, MediaType type, string title, string? description = null)
        => await service.CreateAsync(new Media { MediaType = type, Title = title, Description = description });

    [Fact]
    public async Task CreateAsync_Sets_Timestamps()
    {
        using var database = new TestDatabase();
        var service = new MediaService(database);

        var created = await SeedAsync(service, MediaType.Movie, "Spirited Away");

        Assert.True(created.Id > 0);
        Assert.NotEqual(default, created.CreatedAt);
        Assert.Equal(created.CreatedAt, created.UpdatedAt);
    }

    [Fact]
    public async Task GetLibraryAsync_Filters_By_MediaType()
    {
        using var database = new TestDatabase();
        var service = new MediaService(database);
        await SeedAsync(service, MediaType.Anime, "Bocchi the Rock!");
        await SeedAsync(service, MediaType.Movie, "Your Name");
        await SeedAsync(service, MediaType.TvSeries, "Breaking Bad");

        var anime = await service.GetLibraryAsync(MediaType.Anime);

        Assert.Single(anime);
        Assert.Equal("Bocchi the Rock!", anime[0].Title);
    }

    [Fact]
    public async Task GetLibraryAsync_Search_Matches_Title_And_Description()
    {
        using var database = new TestDatabase();
        var service = new MediaService(database);
        await SeedAsync(service, MediaType.Anime, "Frieren", "A fantasy adventure about an elf mage.");
        await SeedAsync(service, MediaType.Anime, "Vinland Saga", "A historical drama.");

        var byTitle = await service.GetLibraryAsync(MediaType.Anime, "frieren");
        var byDescription = await service.GetLibraryAsync(MediaType.Anime, "historical");

        Assert.Single(byTitle);
        Assert.Equal("Frieren", byTitle[0].Title);
        Assert.Single(byDescription);
        Assert.Equal("Vinland Saga", byDescription[0].Title);
    }

    [Fact]
    public async Task CountAsync_And_RecentlyAdded_Work()
    {
        using var database = new TestDatabase();
        var service = new MediaService(database);
        await SeedAsync(service, MediaType.Anime, "One");
        await SeedAsync(service, MediaType.Anime, "Two");
        await SeedAsync(service, MediaType.Movie, "Three");

        Assert.Equal(2, await service.CountAsync(MediaType.Anime));
        Assert.Equal(1, await service.CountAsync(MediaType.Movie));

        var recent = await service.GetRecentlyAddedAsync(2);
        Assert.Equal(2, recent.Count);
        Assert.Equal("Three", recent[0].Title); // newest first
    }

    [Fact]
    public async Task DeleteAsync_Removes_Media_And_Its_Episodes()
    {
        using var database = new TestDatabase();
        var service = new MediaService(database);

        var media = await service.CreateAsync(new Media
        {
            MediaType = MediaType.Anime,
            Title = "With episodes",
            Episodes = { new Episode { EpisodeNumber = 1 }, new Episode { EpisodeNumber = 2 } },
        });

        await service.DeleteAsync(media.Id);

        Assert.Null(await service.GetByIdAsync(media.Id));
        await using var db = database.CreateDbContext();
        Assert.Empty(db.Episodes);
    }

    [Fact]
    public async Task UpdateAsync_Advances_UpdatedAt_But_Keeps_CreatedAt()
    {
        using var database = new TestDatabase();
        var service = new MediaService(database);
        var media = await SeedAsync(service, MediaType.Anime, "Original");
        var originalCreatedAt = media.CreatedAt;

        media.Title = "Renamed";
        await Task.Delay(5);
        await service.UpdateAsync(media);

        var reloaded = await service.GetByIdAsync(media.Id);
        Assert.NotNull(reloaded);
        Assert.Equal("Renamed", reloaded!.Title);
        Assert.Equal(originalCreatedAt, reloaded.CreatedAt);
        Assert.True(reloaded.UpdatedAt >= originalCreatedAt);
    }
}
