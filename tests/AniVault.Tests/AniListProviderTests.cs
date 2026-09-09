using System.Threading;
using System.Threading.Tasks;
using AniVault.Metadata.Providers;
using AniVault.Models;

namespace AniVault.Tests;

public class AniListProviderTests
{
    private const string SearchJson = """
    {
      "data": {
        "Page": {
          "media": [
            {
              "id": 154587,
              "seasonYear": 2023,
              "title": { "romaji": "Sousou no Frieren", "english": "Frieren", "native": "葬送のフリーレン" },
              "coverImage": { "medium": "https://img/frieren.jpg" },
              "description": "A <b>fantasy</b> adventure."
            }
          ]
        }
      }
    }
    """;

    private const string DetailsJson = """
    {
      "data": {
        "Media": {
          "id": 154587,
          "seasonYear": 2023,
          "season": "FALL",
          "episodes": 28,
          "duration": 24,
          "countryOfOrigin": "JP",
          "averageScore": 94,
          "siteUrl": "https://anilist.co/anime/154587",
          "bannerImage": "https://img/banner.jpg",
          "title": { "romaji": "Sousou no Frieren", "english": "Frieren", "native": "葬送のフリーレン" },
          "synonyms": ["Frieren at the Funeral"],
          "coverImage": { "extraLarge": "https://img/frieren-xl.jpg", "large": "https://img/frieren-l.jpg" },
          "description": "A fantasy adventure about an elf mage.",
          "startDate": { "year": 2023, "month": 9, "day": 29 },
          "endDate": { "year": 2024, "month": 3, "day": 22 },
          "genres": ["Adventure", "Drama", "Fantasy"]
        }
      }
    }
    """;

    [Fact]
    public async Task Search_Maps_Results()
    {
        var factory = new FakeHttpClientFactory().On("anilist.co", SearchJson);
        var provider = new AniListProvider(factory);

        var results = await provider.SearchAsync("frieren", MediaType.Anime, CancellationToken.None);

        Assert.Single(results);
        Assert.Equal("Sousou no Frieren", results[0].Title);
        Assert.Equal(2023, results[0].Year);
        Assert.Equal(MediaType.Anime, results[0].MediaType);
        Assert.Equal("154587", results[0].ExternalId);
        Assert.DoesNotContain("<b>", results[0].Summary);
    }

    [Fact]
    public async Task GetDetails_Maps_Full_Metadata()
    {
        var factory = new FakeHttpClientFactory().On("anilist.co", DetailsJson);
        var provider = new AniListProvider(factory);

        var meta = await provider.GetDetailsAsync("154587", CancellationToken.None);

        Assert.NotNull(meta);
        Assert.Equal(ExternalSource.AniList, meta!.Source);
        Assert.Equal(MediaType.Anime, meta.MediaType);
        Assert.Equal(2023, meta.AirYear);
        Assert.Equal(AnimeSeason.Fall, meta.AirSeason);
        Assert.Equal(28, meta.EpisodeCount);
        Assert.Equal(24, meta.RuntimeMinutes);
        Assert.Equal(9.4, meta.ProviderRating);
        Assert.Equal(new System.DateOnly(2023, 9, 29), meta.StartDate);
        Assert.Contains("Fantasy", meta.Genres);
        Assert.Contains("Frieren at the Funeral", meta.AlternativeTitles);
    }
}
