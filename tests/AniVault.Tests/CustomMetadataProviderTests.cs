using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AniVault.Metadata;
using AniVault.Metadata.Providers;
using AniVault.Models;
using AniVault.Services;

namespace AniVault.Tests;

public class CustomMetadataProviderTests
{
    private sealed class FakeStore : ICustomProviderStore
    {
        public FakeStore(CustomProviderConfig config) => Current = config;
        public CustomProviderConfig Current { get; private set; }
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveAsync(CustomProviderConfig config, CancellationToken cancellationToken = default)
        {
            Current = config;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSecure : ISecureSettingsService
    {
        public readonly Dictionary<string, string?> Values = new();
        public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
            => Task.FromResult(Values.GetValueOrDefault(key));
        public Task SetAsync(string key, string? value, CancellationToken cancellationToken = default)
        {
            Values[key] = value;
            return Task.CompletedTask;
        }
    }

    private static CustomProviderConfig BaseConfig() => new()
    {
        Name = "My API",
        SearchUrl = "https://example.com/search?q={query}",
        MediaType = "Anime",
        ResultsPath = "data",
        IdField = "id",
        TitleField = "name",
        YearField = "released",
        SummaryField = "overview",
    };

    private static CustomMetadataProvider Make(CustomProviderConfig config, FakeHttpClientFactory http, FakeSecure? secure = null)
        => new(http, new FakeStore(config), secure ?? new FakeSecure());

    [Fact]
    public async Task Unconfigured_Provider_Refuses_To_Search()
    {
        var provider = Make(new CustomProviderConfig(), new FakeHttpClientFactory());
        Assert.False(provider.IsConfigured);

        await Assert.ThrowsAsync<MetadataProviderException>(
            () => provider.SearchAsync("frieren", MediaType.Anime, CancellationToken.None));
    }

    [Fact]
    public async Task Search_Maps_Results_By_Configured_Paths()
    {
        const string json = """
        { "data": [ { "id": 7, "name": "Frieren", "released": "2023-09-29", "overview": "An elf mage." } ] }
        """;
        var provider = Make(BaseConfig(), new FakeHttpClientFactory().On("example.com/search", json));

        var results = await provider.SearchAsync("frieren", MediaType.Anime, CancellationToken.None);

        Assert.Single(results);
        Assert.Equal("7", results[0].ExternalId);
        Assert.Equal("Frieren", results[0].Title);
        Assert.Equal(2023, results[0].Year);
        Assert.Equal(ExternalSource.Custom, results[0].Source);
        Assert.Equal(MediaType.Anime, results[0].MediaType);
    }

    [Fact]
    public async Task GetDetails_Without_A_Details_Url_Reuses_The_Search_Row()
    {
        const string json = """
        { "data": [ { "id": 7, "name": "Frieren", "released": "2023", "overview": "An elf mage." } ] }
        """;
        var provider = Make(BaseConfig(), new FakeHttpClientFactory().On("example.com/search", json));

        await provider.SearchAsync("frieren", MediaType.Anime, CancellationToken.None);
        var meta = await provider.GetDetailsAsync("7", CancellationToken.None);

        Assert.NotNull(meta);
        Assert.Equal("Frieren", meta!.Title);
        Assert.Equal("An elf mage.", meta.Description);
        Assert.Equal(2023, meta.AirYear);
    }

    [Fact]
    public async Task GetDetails_With_A_Details_Url_Fetches_And_Maps_Rich_Fields()
    {
        var config = BaseConfig();
        config.DetailsUrl = "https://example.com/item/{id}";
        config.DescriptionField = "description";
        config.EpisodeCountField = "episodes";
        config.RatingField = "score";
        config.GenresPath = "genres";
        config.GenreNameField = "title";

        const string details = """
        {
          "id": 7, "name": "Frieren", "released": "2023-09-29",
          "description": "A fantasy adventure.",
          "episodes": 28, "score": 9.1,
          "genres": [ { "title": "Fantasy" }, { "title": "Adventure" } ]
        }
        """;

        var http = new FakeHttpClientFactory()
            .On("example.com/search", """{ "data": [ { "id": 7, "name": "Frieren" } ] }""")
            .On("example.com/item/7", details);
        var provider = Make(config, http);

        await provider.SearchAsync("frieren", MediaType.Anime, CancellationToken.None);
        var meta = await provider.GetDetailsAsync("7", CancellationToken.None);

        Assert.NotNull(meta);
        Assert.Equal("A fantasy adventure.", meta!.Description);
        Assert.Equal(28, meta.EpisodeCount);
        Assert.Equal(9.1, meta.ProviderRating);
        Assert.Contains("Fantasy", meta.Genres);
        Assert.Contains("Adventure", meta.Genres);
    }

    [Fact]
    public async Task Sends_The_Api_Key_Header_When_Configured()
    {
        var config = BaseConfig();
        config.ApiKeyHeader = "X-API-Key";
        var secure = new FakeSecure();
        secure.Values[CustomMetadataProvider.ApiKeySettingKey] = "secret";

        string? seenHeader = null;
        var http = new FakeHttpClientFactory().OnRequest("example.com/search", req =>
        {
            seenHeader = req.Headers.TryGetValues("X-API-Key", out var v) ? string.Join(",", v) : null;
            return """{ "data": [] }""";
        });

        var provider = Make(config, http, secure);
        await provider.SearchAsync("x", MediaType.Anime, CancellationToken.None);

        Assert.Equal("secret", seenHeader);
    }
}
