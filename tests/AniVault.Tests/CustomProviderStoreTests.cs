using System.Threading.Tasks;
using AniVault.Metadata;
using AniVault.Services;

namespace AniVault.Tests;

public class CustomProviderStoreTests
{
    [Fact]
    public async Task Save_Then_Load_Round_Trips_The_Config()
    {
        using var db = new TestDatabase();

        var config = new CustomProviderConfig
        {
            Name = "My anime API",
            SearchUrl = "https://example.com/anime?q={query}",
            DetailsUrl = "https://example.com/anime/{id}",
            MediaType = "Anime",
            ResultsPath = "data.results",
            IdField = "id",
            TitleField = "attributes.title",
            GenresPath = "attributes.genres",
        };

        await new CustomProviderStore(new SettingsService(db)).SaveAsync(config);

        var reloaded = new CustomProviderStore(new SettingsService(db));
        await reloaded.LoadAsync();

        Assert.True(reloaded.Current.IsConfigured);
        Assert.Equal("My anime API", reloaded.Current.Name);
        Assert.Equal("https://example.com/anime/{id}", reloaded.Current.DetailsUrl);
        Assert.Equal("attributes.title", reloaded.Current.TitleField);
        Assert.Equal(Models.MediaType.Anime, reloaded.Current.ResolveMediaType());
    }

    [Fact]
    public async Task A_Fresh_Store_Is_Not_Configured()
    {
        using var db = new TestDatabase();
        var store = new CustomProviderStore(new SettingsService(db));
        await store.LoadAsync();

        Assert.False(store.Current.IsConfigured);
    }

    [Fact]
    public void IsConfigured_Requires_The_Query_Token()
    {
        Assert.False(new CustomProviderConfig { Name = "x", SearchUrl = "https://example.com/search" }.IsConfigured);
        Assert.True(new CustomProviderConfig { Name = "x", SearchUrl = "https://example.com/search?q={query}" }.IsConfigured);
    }
}
