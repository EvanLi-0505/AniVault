using System.Linq;
using System.Threading.Tasks;
using AniVault.Models;
using AniVault.Services;

namespace AniVault.Tests;

public class MediaQueryServiceTests
{
    private static async Task SeedAsync(TestDatabase db)
    {
        await using var ctx = db.CreateDbContext();
        var fantasy = new Tag { Name = "Fantasy", NormalizedName = "fantasy" };
        var drama = new Tag { Name = "Drama", NormalizedName = "drama" };

        var frieren = new Media
        {
            MediaType = MediaType.Anime, Title = "Frieren", Status = WatchStatus.Completed,
            AirYear = 2023, AirSeason = AnimeSeason.Fall, AirMonth = 9, MyRating = 9.5, IsFavorite = true,
        };
        frieren.MediaTags.Add(new MediaTag { Tag = fantasy });
        frieren.MediaTags.Add(new MediaTag { Tag = drama });

        var vinland = new Media
        {
            MediaType = MediaType.Anime, Title = "Vinland Saga", Status = WatchStatus.Watching,
            AirYear = 2019, AirSeason = AnimeSeason.Summer, AirMonth = 7, MyRating = 8.0,
        };
        vinland.MediaTags.Add(new MediaTag { Tag = drama });

        var yourName = new Media
        {
            MediaType = MediaType.Movie, Title = "Your Name", Status = WatchStatus.Completed,
            AirYear = 2016, MyRating = 9.0, IsFavorite = true,
        };

        ctx.Media.AddRange(frieren, vinland, yourName);
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task ShowOnHome_Filter_Excludes_Items_The_User_Hid_From_Home()
    {
        using var db = new TestDatabase();
        await SeedAsync(db);
        var service = new MediaQueryService(db);

        await using (var ctx = db.CreateDbContext())
        {
            var vinland = ctx.Media.Single(m => m.Title == "Vinland Saga");
            vinland.ShowOnHome = false;
            await ctx.SaveChangesAsync();
        }

        var shown = await service.QueryAsync(new MediaFilter { ShowOnHome = true }, MediaSortOption.Default);
        var hidden = await service.QueryAsync(new MediaFilter { ShowOnHome = false }, MediaSortOption.Default);
        var everyone = await service.QueryAsync(new MediaFilter(), MediaSortOption.Default);

        Assert.DoesNotContain(shown, m => m.Title == "Vinland Saga");
        Assert.Equal(2, shown.Count);
        Assert.Single(hidden);
        Assert.Equal("Vinland Saga", hidden[0].Title);
        Assert.Equal(3, everyone.Count);
    }

    [Fact]
    public async Task Combined_Filters_Narrow_Results()
    {
        using var db = new TestDatabase();
        await SeedAsync(db);
        var service = new MediaQueryService(db);

        var result = await service.QueryAsync(
            new MediaFilter
            {
                MediaType = MediaType.Anime,
                Status = WatchStatus.Completed,
                IsFavorite = true,
                MinRating = 9.0,
                Year = 2023,
            },
            MediaSortOption.Default);

        Assert.Single(result);
        Assert.Equal("Frieren", result[0].Title);
    }

    [Fact]
    public async Task Month_Filter_Matches_AirMonth_Independent_Of_Season()
    {
        using var db = new TestDatabase();
        await SeedAsync(db);
        var service = new MediaQueryService(db);

        var september = await service.QueryAsync(new MediaFilter { Month = 9 }, MediaSortOption.Default);
        var july = await service.QueryAsync(new MediaFilter { Month = 7 }, MediaSortOption.Default);
        var december = await service.QueryAsync(new MediaFilter { Month = 12 }, MediaSortOption.Default);

        Assert.Single(september);
        Assert.Equal("Frieren", september[0].Title);
        Assert.Single(july);
        Assert.Equal("Vinland Saga", july[0].Title);
        Assert.Empty(december);
    }

    [Fact]
    public async Task Text_Search_Also_Matches_Tag_Names()
    {
        using var db = new TestDatabase();
        await SeedAsync(db);
        var service = new MediaQueryService(db);

        var result = await service.QueryAsync(new MediaFilter { Text = "fantasy" }, MediaSortOption.Default);

        Assert.Single(result);
        Assert.Equal("Frieren", result[0].Title);
    }

    [Fact]
    public async Task MatchAllTags_Requires_Every_Tag()
    {
        using var db = new TestDatabase();
        await SeedAsync(db);
        var service = new MediaQueryService(db);

        await using var ctx = db.CreateDbContext();
        var fantasyId = ctx.Tags.Single(t => t.NormalizedName == "fantasy").Id;
        var dramaId = ctx.Tags.Single(t => t.NormalizedName == "drama").Id;

        var all = await service.QueryAsync(
            new MediaFilter { TagIds = new[] { fantasyId, dramaId }, MatchAllTags = true },
            MediaSortOption.Default);
        var any = await service.QueryAsync(
            new MediaFilter { TagIds = new[] { fantasyId, dramaId }, MatchAllTags = false },
            MediaSortOption.Default);

        Assert.Single(all);                 // only Frieren has both
        Assert.Equal(2, any.Count);         // Frieren + Vinland Saga have at least one
    }

    [Fact]
    public async Task Sort_By_Rating_Descending()
    {
        using var db = new TestDatabase();
        await SeedAsync(db);
        var service = new MediaQueryService(db);

        var result = await service.QueryAsync(
            new MediaFilter(),
            new MediaSortOption(MediaSortField.MyRating, Descending: true));

        Assert.Equal(new[] { "Frieren", "Your Name", "Vinland Saga" }, result.Select(m => m.Title).ToArray());
    }

    [Fact]
    public async Task GetBroadcastYears_Are_Distinct_And_Descending()
    {
        using var db = new TestDatabase();
        await SeedAsync(db);
        var service = new MediaQueryService(db);

        var years = await service.GetBroadcastYearsAsync(MediaType.Anime);
        Assert.Equal(new[] { 2023, 2019 }, years.ToArray());
    }
}
