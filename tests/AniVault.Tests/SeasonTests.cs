using System;
using System.Linq;
using System.Threading.Tasks;
using AniVault.Models;
using AniVault.Services;
using AniVault.Utilities;

namespace AniVault.Tests;

public class SeasonTests
{
    [Theory]
    [InlineData(1, AnimeSeason.Winter)]
    [InlineData(2, AnimeSeason.Winter)]
    [InlineData(3, AnimeSeason.Spring)]
    [InlineData(5, AnimeSeason.Spring)]
    [InlineData(6, AnimeSeason.Summer)]
    [InlineData(8, AnimeSeason.Summer)]
    [InlineData(9, AnimeSeason.Fall)]
    [InlineData(11, AnimeSeason.Fall)]
    [InlineData(12, AnimeSeason.Winter)]
    public void SeasonHelper_Maps_Months(int month, AnimeSeason expected)
        => Assert.Equal(expected, SeasonHelper.ForMonth(month));

    [Fact]
    public void SeasonHelper_ForDate_Handles_Summer_And_December_Rollover()
    {
        Assert.Equal((2026, AnimeSeason.Summer), SeasonHelper.ForDate(new DateOnly(2026, 8, 3)));
        Assert.Equal((2023, AnimeSeason.Fall), SeasonHelper.ForDate(new DateOnly(2023, 9, 29)));
        Assert.Equal((2027, AnimeSeason.Winter), SeasonHelper.ForDate(new DateOnly(2026, 12, 15)));
    }

    [Fact]
    public async Task Season_Buckets_Group_By_Year_And_Season()
    {
        using var db = new TestDatabase();
        await using (var ctx = db.CreateDbContext())
        {
            ctx.Media.AddRange(
                new Media { MediaType = MediaType.Anime, Title = "A", AirYear = 2026, AirSeason = AnimeSeason.Summer },
                new Media { MediaType = MediaType.Anime, Title = "B", AirYear = 2026, AirSeason = AnimeSeason.Summer },
                new Media { MediaType = MediaType.Anime, Title = "C", AirYear = 2026, AirSeason = AnimeSeason.Winter },
                new Media { MediaType = MediaType.Anime, Title = "D", AirYear = 2023, AirSeason = AnimeSeason.Fall },
                new Media { MediaType = MediaType.Anime, Title = "E", AirYear = null, AirSeason = null },
                new Media { MediaType = MediaType.Movie, Title = "F", AirYear = 2026 });
            await ctx.SaveChangesAsync();
        }

        var buckets = await new MediaQueryService(db).GetAnimeSeasonBucketsAsync();

        Assert.Equal(3, buckets.Count); // (2026,Summer), (2026,Winter), (2023,Fall) — movie and undated excluded
        Assert.Equal(2, buckets.Single(b => b.Year == 2026 && b.Season == AnimeSeason.Summer).Count);
        Assert.Equal(2026, buckets[0].Year); // newest first
    }
}
