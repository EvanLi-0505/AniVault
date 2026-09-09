using System.Threading.Tasks;
using AniVault.Models;
using AniVault.Services;

namespace AniVault.Tests;

public class ThemeServiceTests
{
    [Fact]
    public async Task Theme_Defaults_To_Dark_And_Persists_Changes()
    {
        using var db = new TestDatabase();
        var settings = new SettingsService(db);
        var service = new ThemeService(settings);

        await service.InitializeAsync();
        Assert.Equal(AppTheme.Dark, service.Current);

        await service.SetThemeAsync(AppTheme.Light);
        Assert.Equal(AppTheme.Light, service.Current);
        Assert.Equal("Light", await settings.GetAsync(SettingKeys.Theme));

        // A fresh service reads the saved value.
        var reloaded = new ThemeService(settings);
        await reloaded.InitializeAsync();
        Assert.Equal(AppTheme.Light, reloaded.Current);
    }
}
