using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AniVault.Metadata;
using AniVault.Models;
using AniVault.Services;

namespace AniVault.Tests;

public class MetadataServiceTests
{
    private sealed class StubProvider : IMetadataProvider
    {
        public StubProvider(ExternalSource source, bool requiresKey = false)
        {
            Source = source;
            RequiresApiKey = requiresKey;
        }

        public ExternalSource Source { get; }
        public string DisplayName => Source.ToString();
        public string Description => "stub";
        public bool RequiresApiKey { get; }
        public bool SupportsMediaType(MediaType mediaType) => true;

        public Task<IReadOnlyList<MetadataSearchResult>> SearchAsync(string query, MediaType? preferredMediaType, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<MetadataSearchResult>>(new[]
            {
                new MetadataSearchResult { Source = Source, ExternalId = "1", Title = $"{Source}:{query}" },
            });

        public Task<MediaMetadata?> GetDetailsAsync(string externalId, CancellationToken cancellationToken)
            => Task.FromResult<MediaMetadata?>(new MediaMetadata { Source = Source, ExternalId = externalId, Title = "x" });
    }

    private static MetadataService Create(TestDatabase db, params IMetadataProvider[] providers)
    {
        var settings = new SettingsService(db);
        return new MetadataService(providers, settings, new SecureSettingsService(settings));
    }

    [Fact]
    public async Task Search_Is_Blocked_Until_Online_Search_Is_Enabled()
    {
        using var db = new TestDatabase();
        var service = Create(db, new StubProvider(ExternalSource.AniList));

        await Assert.ThrowsAsync<MetadataProviderException>(
            () => service.SearchAsync(ExternalSource.AniList, "frieren", MediaType.Anime, CancellationToken.None));

        await new SettingsService(db).SetAsync(SettingKeys.OnlineSearchEnabled, "true");

        var results = await service.SearchAsync(ExternalSource.AniList, "frieren", MediaType.Anime, CancellationToken.None);
        Assert.Single(results);
    }

    [Fact]
    public async Task Active_Provider_Round_Trips_And_Defaults_To_Bangumi()
    {
        using var db = new TestDatabase();
        var service = Create(db, new StubProvider(ExternalSource.AniList), new StubProvider(ExternalSource.Bangumi));

        Assert.Equal(ExternalSource.Bangumi, await service.GetActiveProviderAsync());

        await service.SetActiveProviderAsync(ExternalSource.AniList);
        Assert.Equal(ExternalSource.AniList, await service.GetActiveProviderAsync());
    }

    [Fact]
    public async Task Provider_Requiring_A_Key_Reports_Not_Ready_Until_Key_Is_Set()
    {
        using var db = new TestDatabase();
        var service = Create(db, new StubProvider(ExternalSource.Tmdb, requiresKey: true));

        var before = (await service.GetProvidersAsync()).Single();
        Assert.False(before.IsReady);

        await service.SetApiKeyAsync(ExternalSource.Tmdb, "secret-key");

        var after = (await service.GetProvidersAsync()).Single();
        Assert.True(after.IsReady);
        Assert.Equal("secret-key", await service.GetApiKeyAsync(ExternalSource.Tmdb));
    }
}
