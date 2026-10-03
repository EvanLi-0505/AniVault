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
    public async Task New_Media_Defaults_To_Shown_On_Home()
    {
        using var database = new TestDatabase();
        var service = new MediaService(database);

        var created = await SeedAsync(service, MediaType.Anime, "Frieren");

        Assert.True(created.ShowOnHome);
    }

    [Fact]
    public async Task SetShowOnHomeAsync_Persists_The_Flag()
    {
        using var database = new TestDatabase();
        var service = new MediaService(database);
        var media = await SeedAsync(service, MediaType.Anime, "Frieren");

        await service.SetShowOnHomeAsync(media.Id, false);

        var reloaded = await service.GetByIdAsync(media.Id);
        Assert.NotNull(reloaded);
        Assert.False(reloaded!.ShowOnHome);
    }

    [Fact]
    public async Task SetEpisodeListHiddenAsync_Persists_Without_Touching_UpdatedAt_Or_Episodes()
    {
        using var database = new TestDatabase();
        var service = new MediaService(database);
        var media = await SeedAsync(service, MediaType.Anime, "Frieren");
        await service.SyncEpisodeListAsync(media.Id, 3);
        var before = await service.GetByIdAsync(media.Id);
        Assert.False(before!.EpisodeListHidden);

        await service.SetEpisodeListHiddenAsync(media.Id, true);

        var after = await service.GetByIdAsync(media.Id);
        Assert.True(after!.EpisodeListHidden);
        Assert.Equal(before.UpdatedAt, after.UpdatedAt);
        Assert.Equal(3, after.Episodes.Count);
    }

    [Fact]
    public async Task GetRecentlyAddedAsync_Excludes_Items_Hidden_From_Home()
    {
        using var database = new TestDatabase();
        var service = new MediaService(database);
        var shown = await SeedAsync(service, MediaType.Anime, "Shown");
        var hidden = await SeedAsync(service, MediaType.Anime, "Hidden");
        await service.SetShowOnHomeAsync(hidden.Id, false);

        var recent = await service.GetRecentlyAddedAsync(10);

        Assert.Contains(recent, m => m.Id == shown.Id);
        Assert.DoesNotContain(recent, m => m.Id == hidden.Id);
    }

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
