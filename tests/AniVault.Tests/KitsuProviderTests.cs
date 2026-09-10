using System.Threading;
using System.Threading.Tasks;
using AniVault.Metadata.Providers;
using AniVault.Models;

namespace AniVault.Tests;

public class KitsuProviderTests
{
    private const string SearchJson = """
    {
      "data": [
        {
          "id": "44081",
          "type": "anime",
          "attributes": {
            "canonicalTitle": "Sousou no Frieren",
            "titles": { "en": "Frieren: Beyond Journey's End", "ja_jp": "葬送のフリーレン" },
            "synopsis": "The adventure is over but life goes on...",
            "startDate": "2023-09-29",
            "posterImage": { "medium": "https://img/frieren-m.jpg" }
          }
        }
      ]
    }
    """;

    private const string DetailsJson = """
    {
      "data": {
        "id": "44081",
        "type": "anime",
        "attributes": {
          "canonicalTitle": "Sousou no Frieren",
          "titles": { "en": "Frieren: Beyond Journey's End", "ja_jp": "葬送のフリーレン" },
          "synopsis": "The adventure is over but life goes on...",
          "startDate": "2023-09-29",
          "endDate": "2024-03-22",
          "episodeCount": 28,
          "episodeLength": 24,
          "averageRating": "85.2",
          "posterImage": { "original": "https://img/frieren.jpg" }
        }
      },
      "included": [
        { "type": "genres", "attributes": { "name": "Fantasy" } },
        { "type": "categories", "attributes": { "name": "Adventure" } }
      ]
    }
    """;

    [Fact]
    public async Task Search_Maps_Results()
    {
        var provider = new KitsuProvider(new FakeHttpClientFactory().On("kitsu.io", SearchJson));

        var results = await provider.SearchAsync("frieren", MediaType.Anime, CancellationToken.None);

        Assert.Single(results);
        Assert.Equal("44081", results[0].ExternalId);
        Assert.Equal("Frieren: Beyond Journey's End", results[0].Title);
        Assert.Equal("葬送のフリーレン", results[0].SecondaryTitle);
        Assert.Equal(2023, results[0].Year);
        Assert.Equal(ExternalSource.Kitsu, results[0].Source);
    }

    [Fact]
    public async Task GetDetails_Maps_Full_Metadata_And_Scales_Rating()
    {
        var provider = new KitsuProvider(new FakeHttpClientFactory().On("kitsu.io", DetailsJson));

        var meta = await provider.GetDetailsAsync("44081", CancellationToken.None);

        Assert.NotNull(meta);
        Assert.Equal(28, meta!.EpisodeCount);
        Assert.Equal(24, meta.RuntimeMinutes);
        Assert.Equal(8.5, meta.ProviderRating);        // 85.2 / 10, rounded
        Assert.Equal(new System.DateOnly(2023, 9, 29), meta.StartDate);
        Assert.Equal(new System.DateOnly(2024, 3, 22), meta.EndDate);
        Assert.Contains("Fantasy", meta.Genres);
        Assert.Contains("Adventure", meta.Genres);
    }
}
