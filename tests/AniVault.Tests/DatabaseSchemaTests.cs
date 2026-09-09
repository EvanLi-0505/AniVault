using System.Linq;
using System.Threading.Tasks;
using AniVault.Models;
using Microsoft.EntityFrameworkCore;

namespace AniVault.Tests;

public class DatabaseSchemaTests
{
    [Fact]
    public async Task Migrations_Apply_And_Media_Persists_Across_Contexts()
    {
        using var database = new TestDatabase();

        int mediaId;
        await using (var db = database.CreateDbContext())
        {
            var media = new Media
            {
                MediaType = MediaType.Anime,
                Title = "Frieren",
                AirYear = 2023,
                AirSeason = AnimeSeason.Fall,
                Status = WatchStatus.Completed,
            };
            db.Media.Add(media);
            await db.SaveChangesAsync();
            mediaId = media.Id;
        }

        await using (var db = database.CreateDbContext())
        {
            var reloaded = await db.Media.SingleAsync(m => m.Id == mediaId);
            Assert.Equal("Frieren", reloaded.Title);
            Assert.Equal(AnimeSeason.Fall, reloaded.AirSeason);
            Assert.Equal(WatchStatus.Completed, reloaded.Status);
        }
    }

    [Fact]
    public async Task Deleting_Media_Cascades_To_Episodes_Tags_And_ExternalIds()
    {
        using var database = new TestDatabase();

        int mediaId;
        await using (var db = database.CreateDbContext())
        {
            var tag = new Tag { Name = "Fantasy", NormalizedName = "fantasy" };
            var media = new Media
            {
                MediaType = MediaType.Anime,
                Title = "Test",
                Episodes = { new Episode { EpisodeNumber = 1 }, new Episode { EpisodeNumber = 2 } },
                ExternalIds = { new MediaExternalId { Source = ExternalSource.AniList, ExternalId = "999" } },
            };
            media.MediaTags.Add(new MediaTag { Tag = tag });
            db.Media.Add(media);
            await db.SaveChangesAsync();
            mediaId = media.Id;
        }

        await using (var db = database.CreateDbContext())
        {
            db.Media.Remove(await db.Media.SingleAsync(m => m.Id == mediaId));
            await db.SaveChangesAsync();
        }

        await using (var db = database.CreateDbContext())
        {
            Assert.Empty(db.Episodes);
            Assert.Empty(db.MediaExternalIds);
            Assert.Empty(db.MediaTags);
            Assert.Single(db.Tags); // the tag itself survives; only the link is removed
        }
    }

    [Fact]
    public async Task Tag_NormalizedName_Is_Unique()
    {
        using var database = new TestDatabase();
        await using var db = database.CreateDbContext();

        db.Tags.Add(new Tag { Name = "Isekai", NormalizedName = "isekai" });
        db.Tags.Add(new Tag { Name = "isekai", NormalizedName = "isekai" });

        await Assert.ThrowsAnyAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Duplicate_Provider_Id_Is_Rejected()
    {
        using var database = new TestDatabase();
        await using var db = database.CreateDbContext();

        db.Media.Add(new Media
        {
            MediaType = MediaType.Anime,
            Title = "A",
            ExternalIds = { new MediaExternalId { Source = ExternalSource.Bangumi, ExternalId = "12345" } },
        });
        db.Media.Add(new Media
        {
            MediaType = MediaType.Anime,
            Title = "B",
            ExternalIds = { new MediaExternalId { Source = ExternalSource.Bangumi, ExternalId = "12345" } },
        });

        await Assert.ThrowsAnyAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Default_Tags_Table_Starts_Empty_Before_Initializer_Runs()
    {
        using var database = new TestDatabase();
        await using var db = database.CreateDbContext();
        Assert.Empty(db.Tags);
    }
}
