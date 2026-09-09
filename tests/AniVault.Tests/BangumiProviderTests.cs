using System.Threading;
using System.Threading.Tasks;
using AniVault.Metadata.Providers;
using AniVault.Models;

namespace AniVault.Tests;

public class BangumiProviderTests
{
    private const string SearchJson = """
    {
      "data": [
        {
          "id": 400602,
          "type": 2,
          "date": "2023-09-29",
          "name": "葬送のフリーレン",
          "name_cn": "葬送的芙莉莲",
          "summary": "魔王を倒した勇者一行の魔法使いフリーレン。",
          "images": { "common": "https://lain.bgm.tv/pic/cover/c/400602.jpg", "large": "https://lain.bgm.tv/pic/cover/l/400602.jpg" }
        }
      ],
      "total": 1
    }
    """;

    private const string DetailJson = """
    {
      "id": 400602,
      "type": 2,
      "date": "2023-09-29",
      "name": "葬送のフリーレン",
      "name_cn": "葬送的芙莉莲",
      "summary": "魔王を倒した勇者一行の魔法使いフリーレン。",
      "eps": 28,
      "total_episodes": 28,
      "images": { "large": "https://lain.bgm.tv/pic/cover/l/400602.jpg" },
      "rating": { "score": 8.6 },
      "tags": [ { "name": "奇幻" }, { "name": "治愈" } ]
    }
    """;

    [Fact]
    public async Task Search_Prefers_Chinese_Title_And_Maps_Year()
    {
        var provider = new BangumiProvider(new FakeHttpClientFactory().On("api.bgm.tv/v0/search", SearchJson));

        var results = await provider.SearchAsync("frieren", MediaType.Anime, CancellationToken.None);

        Assert.Single(results);
        Assert.Equal("葬送的芙莉莲", results[0].Title);
        Assert.Equal("葬送のフリーレン", results[0].SecondaryTitle);
        Assert.Equal(2023, results[0].Year);
        Assert.Equal(MediaType.Anime, results[0].MediaType);
    }

    [Fact]
    public async Task GetDetails_Maps_Episodes_Rating_Tags_And_Season()
    {
        var provider = new BangumiProvider(new FakeHttpClientFactory().On("api.bgm.tv/v0/subjects/", DetailJson));

        var meta = await provider.GetDetailsAsync("400602", CancellationToken.None);

        Assert.NotNull(meta);
        Assert.Equal(ExternalSource.Bangumi, meta!.Source);
        Assert.Equal(MediaType.Anime, meta.MediaType);
        Assert.Equal("葬送的芙莉莲", meta.Title);
        Assert.Equal(28, meta.EpisodeCount);
        Assert.Equal(8.6, meta.ProviderRating);
        Assert.Equal(2023, meta.AirYear);
        Assert.Equal(AnimeSeason.Fall, meta.AirSeason);
        Assert.Contains("奇幻", meta.Genres);
    }
}
