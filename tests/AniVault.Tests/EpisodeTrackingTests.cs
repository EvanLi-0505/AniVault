using System.Linq;
using System.Threading.Tasks;
using AniVault.Models;
using AniVault.Services;
using Microsoft.EntityFrameworkCore;

namespace AniVault.Tests;

public class EpisodeTrackingTests
{
    [Fact]
    public async Task SyncEpisodeList_Creates_Then_Trims_Episodes_Preserving_Watched_State()
    {
        using var database = new TestDatabase();
        var service = new MediaService(database);

        var media = await service.CreateAsync(new Media { MediaType = MediaType.Anime, Title = "Test" });

        await service.SyncEpisodeListAsync(media.Id, 12);
        var afterCreate = await service.GetByIdAsync(media.Id);
        Assert.Equal(12, afterCreate!.Episodes.Count);

        var episode3 = afterCreate.Episodes.Single(e => e.EpisodeNumber == 3);
        await service.SetEpisodeWatchedAsync(episode3.Id, true);

        await service.SyncEpisodeListAsync(media.Id, 6);
        var afterTrim = await service.GetByIdAsync(media.Id);

        Assert.Equal(6, afterTrim!.Episodes.Count);
        Assert.True(afterTrim.Episodes.Single(e => e.EpisodeNumber == 3).IsWatched);
        Assert.DoesNotContain(afterTrim.Episodes, e => e.EpisodeNumber > 6);
    }

    [Fact]
    public async Task SetStatus_Completed_Stamps_CompletedAt_Once()
    {
        using var database = new TestDatabase();
        var service = new MediaService(database);
        var media = await service.CreateAsync(new Media { MediaType = MediaType.Movie, Title = "Movie" });

        await service.SetStatusAsync(media.Id, WatchStatus.Completed);
        var first = await service.GetByIdAsync(media.Id);
        var stamp = first!.CompletedAt;
        Assert.NotNull(stamp);

        await service.SetStatusAsync(media.Id, WatchStatus.Watching);
        await service.SetStatusAsync(media.Id, WatchStatus.Completed);
        var again = await service.GetByIdAsync(media.Id);

        Assert.Equal(stamp, again!.CompletedAt); // not overwritten
    }

    [Fact]
    public async Task SetRating_Clamps_To_Zero_Ten()
    {
        using var database = new TestDatabase();
        var service = new MediaService(database);
        var media = await service.CreateAsync(new Media { MediaType = MediaType.Anime, Title = "Rate me" });

        await service.SetRatingAsync(media.Id, 42);
        Assert.Equal(10d, (await service.GetByIdAsync(media.Id))!.MyRating);

        await service.SetRatingAsync(media.Id, -3);
        Assert.Equal(0d, (await service.GetByIdAsync(media.Id))!.MyRating);
    }
}
