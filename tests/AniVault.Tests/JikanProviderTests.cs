using System.Threading;
using System.Threading.Tasks;
using AniVault.Metadata.Providers;
using AniVault.Models;

namespace AniVault.Tests;

public class JikanProviderTests
{
    private const string SearchJson = """
    {
      "data": [
        {
          "mal_id": 52991,
          "url": "https://myanimelist.net/anime/52991",
          "images": { "jpg": { "large_image_url": "https://img/frieren.jpg" } },
          "title": "Sousou no Frieren",
          "title_english": "Frieren: Beyond Journey's End",
          "title_japanese": "葬送のフリーレン",
          "year": 2023,
          "synopsis": "During their decade-long quest..."
        }
      ]
    }
    """;

    private const string DetailsJson = """
    {
      "data": {
        "mal_id": 52991,
        "url": "https://myanimelist.net/anime/52991",
        "images": { "jpg": { "large_image_url": "https://img/frieren.jpg" } },
        "title": "Sousou no Frieren",
        "title_english": "Frieren: Beyond Journey's End",
        "title_japanese": "葬送のフリーレン",
        "title_synonyms": ["Frieren"],
        "episodes": 28,
        "duration": "24 min per ep",
        "score": 9.31,
        "year": 2023,
        "season": "fall",
        "aired": { "from": "2023-09-29T00:00:00+00:00", "to": "2024-03-22T00:00:00+00:00" },
        "synopsis": "During their decade-long quest to defeat the Demon King...",
        "genres": [ { "name": "Adventure" }, { "name": "Drama" } ],
        "themes": [ { "name": "Mythology" } ]
      }
    }
    """;

    [Fact]
    public async Task Search_Maps_Results()
    {
        var provider = new JikanProvider(new FakeHttpClientFactory().On("api.jikan.moe", SearchJson));

        var results = await provider.SearchAsync("frieren", MediaType.Anime, CancellationToken.None);

        Assert.Single(results);
        Assert.Equal("52991", results[0].ExternalId);
        Assert.Equal("Frieren: Beyond Journey's End", results[0].Title);
        Assert.Equal(2023, results[0].Year);
        Assert.Equal(MediaType.Anime, results[0].MediaType);
        Assert.Equal(ExternalSource.Jikan, results[0].Source);
    }

    [Fact]
    public async Task GetDetails_Maps_Full_Metadata()
    {
        var provider = new JikanProvider(new FakeHttpClientFactory().On("api.jikan.moe", DetailsJson));

        var meta = await provider.GetDetailsAsync("52991", CancellationToken.None);

        Assert.NotNull(meta);
        Assert.Equal("葬送のフリーレン", meta!.OriginalTitle);
        Assert.Equal(28, meta.EpisodeCount);
        Assert.Equal(24, meta.RuntimeMinutes);
        Assert.Equal(9.31, meta.ProviderRating);
        Assert.Equal(AnimeSeason.Fall, meta.AirSeason);
        Assert.Equal(new System.DateOnly(2023, 9, 29), meta.StartDate);
        Assert.Contains("Adventure", meta.Genres);
        Assert.Contains("Mythology", meta.Genres);
    }
}
