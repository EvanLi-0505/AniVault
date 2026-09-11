using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using AniVault.Models;
using AniVault.Services;
using AniVault.ViewModels;
using AniVault.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AniVault;

/// <summary>
/// The <c>ANIVAULT_SMOKE=1</c> dev/CI aid: visits every page, toggles theme and language, and
/// load-and-closes every modal window, so a broken page, binding, or window template surfaces
/// in the log without any clicking. Kept in its own file — it is test tooling, not part of the
/// app's real startup path (see <c>App.xaml.cs</c> for that).
/// </summary>
public partial class App
{
    /// <summary>
    /// Visits every sidebar page once (English, then Chinese), exercises the theme swap, opens
    /// a media detail page if one exists, then load-and-closes each modal window in turn.
    /// </summary>
    private async Task RunNavigationSmokeTestAsync(MainWindow shell)
    {
        var viewModel = (ShellViewModel)shell.DataContext;
        foreach (var item in viewModel.NavItems.Where(i => i.IsSelectable).ToList())
        {
            viewModel.SelectedNavItem = item;
            await Task.Delay(350);
            _logger!.LogInformation("Smoke: visited '{Page}'.", item.Label);
        }

        // Exercise the runtime theme swap.
        var themes = _services!.GetRequiredService<IThemeService>();
        await themes.SetThemeAsync(AppTheme.Light);
        await Task.Delay(300);
        await themes.SetThemeAsync(AppTheme.Dark);
        _logger!.LogInformation("Smoke: toggled theme.");

        // Exercise the runtime language swap: re-visit every page in Chinese, then restore English.
        var loc = _services!.GetRequiredService<LocalizationService>();
        var originalLanguage = loc.Current;
        await loc.SetLanguageAsync(AppLanguage.Chinese);
        await Task.Delay(200);
        foreach (var item in viewModel.NavItems.Where(i => i.IsSelectable).ToList())
        {
            viewModel.SelectedNavItem = item;
            await Task.Delay(120);
        }

        _logger!.LogInformation("Smoke: visited every page in Chinese.");
        await loc.SetLanguageAsync(originalLanguage);

        // Also open a media detail page if the library has any items.
        var nav = _services!.GetRequiredService<INavigationService>();
        var media = await _services!.GetRequiredService<IMediaService>().GetRecentlyAddedAsync(1);
        if (media.Count > 0)
        {
            nav.NavigateToDetail<MediaDetailViewModel>(vm => vm.MediaId = media[0].Id);
            await Task.Delay(600);
            _logger!.LogInformation("Smoke: opened detail for media {Id}.", media[0].Id);
        }

        // Load-and-close each modal window so a broken window template surfaces in the log
        // (the page tour above never opens these).
        await SmokeShowWindowAsync("Add-media editor", () =>
        {
            var vm = _services!.GetRequiredService<MediaEditorViewModel>();
            _ = vm.InitializeForNewAsync(MediaType.Anime);
            return new MediaEditorWindow { DataContext = vm, Owner = shell };
        });
        await SmokeShowWindowAsync("Online search", () =>
        {
            var vm = _services!.GetRequiredService<OnlineSearchViewModel>();
            _ = vm.InitializeAsync(MediaType.Anime);
            return new OnlineSearchWindow { DataContext = vm, Owner = shell };
        });
        await SmokeShowWindowAsync("First-run", () =>
            new FirstRunWindow { DataContext = _services!.GetRequiredService<FirstRunViewModel>(), Owner = shell });
        await SmokeShowWindowAsync("Custom provider", () =>
        {
            var vm = _services!.GetRequiredService<CustomProviderViewModel>();
            _ = vm.LoadAsync();
            return new CustomProviderWindow { DataContext = vm, Owner = shell };
        });
        await SmokeShowWindowAsync("Rating questionnaire", () => new RatingCalculatorWindow
        {
            DataContext = _services!.GetRequiredService<RatingCalculatorViewModel>(),
            Owner = shell,
        });
        await SmokeShowWindowAsync("Rating rubric", () => new RatingGuideWindow { Owner = shell });
        await SmokeShowWindowAsync("Compress artwork", () =>
        {
            var vm = _services!.GetRequiredService<CompressArtworkViewModel>();
            _ = vm.LoadAsync();
            return new CompressArtworkWindow { DataContext = vm, Owner = shell };
        });

        // Exercise the themed Calendar / DatePicker drop-down templates in all three display modes.
        await SmokeShowWindowAsync("Calendar", () =>
        {
            var calendar = new System.Windows.Controls.Calendar { SelectedDate = DateTime.Today };
            var window = new Window { Content = calendar, Owner = shell, Width = 300, Height = 300 };
            window.Loaded += async (_, _) =>
            {
                foreach (var mode in new[]
                {
                    System.Windows.Controls.CalendarMode.Year,
                    System.Windows.Controls.CalendarMode.Decade,
                    System.Windows.Controls.CalendarMode.Month,
                })
                {
                    calendar.DisplayMode = mode;
                    await Task.Delay(60);
                }
            };
            return window;
        });

        _logger!.LogInformation("Smoke test complete.");
        Shutdown();
    }

    private async Task SmokeShowWindowAsync(string name, Func<Window> create)
    {
        try
        {
            var window = create();
            window.ShowInTaskbar = false;
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = -10000;
            window.Show();
            await Task.Delay(250);
            window.Close();
            _logger!.LogInformation("Smoke: opened '{Window}'.", name);
        }
        catch (Exception ex)
        {
            _logger!.LogError(ex, "Smoke: window '{Window}' failed to open.", name);
        }
    }
}
