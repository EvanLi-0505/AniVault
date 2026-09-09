using System.Linq;
using System.Threading.Tasks;
using AniVault.Models;
using AniVault.Services;

namespace AniVault.Tests;

public class TagServiceTests
{
    [Fact]
    public async Task GetOrCreate_Is_Case_Insensitive()
    {
        using var db = new TestDatabase();
        var service = new TagService(db);

        var a = await service.GetOrCreateAsync("Isekai");
        var b = await service.GetOrCreateAsync("  isekai ");

        Assert.Equal(a.Id, b.Id);

        await using var ctx = db.CreateDbContext();
        Assert.Equal(1, ctx.Tags.Count(t => t.NormalizedName == "isekai"));
    }

    [Fact]
    public async Task Rename_Rejects_A_Clash()
    {
        using var db = new TestDatabase();
        var service = new TagService(db);
        var action = await service.GetOrCreateAsync("Action");
        await service.GetOrCreateAsync("Adventure");

        await Assert.ThrowsAsync<System.InvalidOperationException>(
            () => service.RenameAsync(action.Id, "adventure"));
    }

    [Fact]
    public async Task SetMediaTags_Adds_Removes_And_Creates()
    {
        using var db = new TestDatabase();
        var tagService = new TagService(db);
        var mediaService = new MediaService(db);

        var media = await mediaService.CreateAsync(new Media { MediaType = MediaType.Anime, Title = "Test" });

        await tagService.SetMediaTagsAsync(media.Id, new[] { "Fantasy", "Drama" });
        var afterAdd = await mediaService.GetByIdAsync(media.Id);
        Assert.Equal(new[] { "Drama", "Fantasy" },
            afterAdd!.MediaTags.Select(mt => mt.Tag!.Name).OrderBy(n => n).ToArray());

        await tagService.SetMediaTagsAsync(media.Id, new[] { "Fantasy", "Comedy" });
        var afterChange = await mediaService.GetByIdAsync(media.Id);
        Assert.Equal(new[] { "Comedy", "Fantasy" },
            afterChange!.MediaTags.Select(mt => mt.Tag!.Name).OrderBy(n => n).ToArray());
    }

    [Fact]
    public async Task DeleteUnused_Only_Removes_Orphans()
    {
        using var db = new TestDatabase();
        var tagService = new TagService(db);
        var mediaService = new MediaService(db);

        var media = await mediaService.CreateAsync(new Media { MediaType = MediaType.Anime, Title = "Test" });
        await tagService.SetMediaTagsAsync(media.Id, new[] { "Used" });
        await tagService.GetOrCreateAsync("Orphan");

        var removed = await tagService.DeleteUnusedAsync();

        Assert.Equal(1, removed);
        var remaining = await tagService.GetAllWithUsageAsync();
        Assert.Equal(new[] { "Used" }, remaining.Select(t => t.Name).ToArray());
    }

    [Fact]
    public async Task GetAllWithUsage_Counts_Media()
    {
        using var db = new TestDatabase();
        var tagService = new TagService(db);
        var mediaService = new MediaService(db);

        var a = await mediaService.CreateAsync(new Media { MediaType = MediaType.Anime, Title = "A" });
        var b = await mediaService.CreateAsync(new Media { MediaType = MediaType.Anime, Title = "B" });
        await tagService.SetMediaTagsAsync(a.Id, new[] { "Shared" });
        await tagService.SetMediaTagsAsync(b.Id, new[] { "Shared", "Solo" });

        var usage = await tagService.GetAllWithUsageAsync();
        Assert.Equal(2, usage.Single(t => t.Name == "Shared").MediaCount);
        Assert.Equal(1, usage.Single(t => t.Name == "Solo").MediaCount);
    }
}
