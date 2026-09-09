using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using AniVault.Models;

namespace AniVault.Services;

public enum AppTheme
{
    Dark,
    Light,
}

/// <summary>
/// Swaps the application's theme resource dictionary at runtime and remembers the choice.
/// Every View references colours with <c>DynamicResource</c>, so a swap is instant.
/// </summary>
public interface IThemeService
{
    AppTheme Current { get; }

    /// <summary>Loads the saved theme and applies it. Call once at startup.</summary>
    Task InitializeAsync();

    Task SetThemeAsync(AppTheme theme);
}

public sealed class ThemeService : IThemeService
{
    private const string ThemeMarker = "Theme.xaml";

    private readonly ISettingsService _settings;

    public ThemeService(ISettingsService settings)
    {
        _settings = settings;
    }

    public AppTheme Current { get; private set; } = AppTheme.Dark;

    public async Task InitializeAsync()
    {
        var saved = await _settings.GetAsync(SettingKeys.Theme, AppTheme.Dark.ToString());
        Apply(Enum.TryParse<AppTheme>(saved, ignoreCase: true, out var theme) ? theme : AppTheme.Dark);
    }

    public async Task SetThemeAsync(AppTheme theme)
    {
        if (theme == Current)
        {
            return;
        }

        Apply(theme);
        await _settings.SetAsync(SettingKeys.Theme, theme.ToString());
    }

    private void Apply(AppTheme theme)
    {
        Current = theme;

        if (Application.Current is null)
        {
            return;
        }

        var uri = new Uri(
            $"pack://application:,,,/Resources/Themes/{theme}Theme.xaml", UriKind.Absolute);
        var dictionary = new ResourceDictionary { Source = uri };

        var merged = Application.Current.Resources.MergedDictionaries;
        var existing = merged.FirstOrDefault(d =>
            d.Source is { } s && s.OriginalString.EndsWith(ThemeMarker, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            merged[merged.IndexOf(existing)] = dictionary;
        }
        else
        {
            merged.Insert(0, dictionary);
        }
    }
}
