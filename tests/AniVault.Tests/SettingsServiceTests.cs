using System.Threading.Tasks;
using AniVault.Models;
using AniVault.Services;

namespace AniVault.Tests;

public class SettingsServiceTests
{
    [Fact]
    public async Task Get_Returns_Default_When_Missing()
    {
        using var database = new TestDatabase();
        var service = new SettingsService(database);

        Assert.Equal("Bangumi", await service.GetAsync(SettingKeys.MetadataProvider, "Bangumi"));
        Assert.False(await service.GetBoolAsync(SettingKeys.OnlineSearchEnabled, false));
    }

    [Fact]
    public async Task Set_Then_Get_RoundTrips_And_Upserts()
    {
        using var database = new TestDatabase();
        var service = new SettingsService(database);

        await service.SetAsync(SettingKeys.MetadataProvider, "AniList");
        await service.SetAsync(SettingKeys.MetadataProvider, "TMDB"); // update existing row
        await service.SetAsync(SettingKeys.OnlineSearchEnabled, true.ToString());

        Assert.Equal("TMDB", await service.GetAsync(SettingKeys.MetadataProvider));
        Assert.True(await service.GetBoolAsync(SettingKeys.OnlineSearchEnabled, false));
    }
}
