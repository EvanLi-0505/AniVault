using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using AniVault.Data;
using AniVault.Services;
using AniVault.Services.Logging;
using AniVault.ViewModels;
using AniVault.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AniVault;

/// <summary>
/// Application entry point. Wires up dependency injection, decides between the first-run
/// setup screen and the main window, and initialises the local database.
///
/// Nothing here touches the network.
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _services;
    private ILogger<App>? _logger;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Keep the process alive across the first-run window closing before the main window opens.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        try
        {
            _services = BuildServiceProvider();
            _logger = _services.GetRequiredService<ILogger<App>>();
            // Instantiate now so LocalizationService.Instance is set before any window XAML parses.
            _services.GetRequiredService<LocalizationService>();
        }
        catch (Exception ex)
        {
            ReportFatalStartupError(ex);
            Shutdown(-1);
            return;
        }

        SetupGlobalExceptionHandlers();
        _logger.LogInformation("AniVault starting (version {Version}).",
            typeof(App).Assembly.GetName().Version);

        // Run the async startup on the dispatcher once the message loop is pumping,
        // so awaits inside it resume correctly (no blocking .GetResult()).
        _ = Dispatcher.InvokeAsync(async () =>
        {
            try
            {
                await StartAsync();
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Startup failed.");
                ReportFatalStartupError(ex);
                Shutdown(-1);
            }
        });
    }

    /// <summary>
    /// Writes the full exception somewhere the user can find it even before a data folder
    /// (and therefore the normal log) exists, then shows a message pointing there.
    /// </summary>
    private static void ReportFatalStartupError(Exception ex)
    {
        var text = $"AniVault {typeof(App).Assembly.GetName().Version} failed to start at "
                   + $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}\r\n\r\n{ex}\r\n";

        string? writtenTo = null;
        foreach (var candidate in new[]
                 {
                     System.IO.Path.Combine(AppContext.BaseDirectory, "AniVault-startup-error.txt"),
                     System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AniVault-startup-error.txt"),
                 })
        {
            try
            {
                System.IO.File.WriteAllText(candidate, text);
                writtenTo = candidate;
                break;
            }
            catch
            {
                // try the next location
            }
        }

        MessageBox.Show(
            "AniVault could not start.\n\n"
            + (writtenTo is null ? ex.Message : $"Details were written to:\n{writtenTo}"),
            "AniVault", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private async Task StartAsync()
    {
        ArgumentNullException.ThrowIfNull(_services);
        var paths = _services.GetRequiredService<IAppPathService>();

        var smoke = Environment.GetEnvironmentVariable("ANIVAULT_SMOKE") == "1";

        if (!paths.IsConfigured)
        {
            if (smoke)
            {
                // CI / smoke: exercise the real first-run path (resolve its ViewModel) without a dialog.
                _ = _services.GetRequiredService<FirstRunViewModel>();
                var temp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"anivault-smoke-{Guid.NewGuid():N}");
                paths.SetDataDirectory(temp);
                _logger!.LogInformation("Smoke: auto-configured data directory at {Path}.", temp);
            }
            else
            {
                var completed = ShowFirstRun();
                if (!completed)
                {
                    _logger!.LogInformation("First-run setup was cancelled. Exiting.");
                    Shutdown();
                    return;
                }
            }
        }

        // Whether configured just now or on a previous run, make sure the layout and
        // schema are current. Migrations are idempotent and never drop user data.
        paths.EnsureDirectoryStructure();
        await _services.GetRequiredService<DatabaseInitializer>().InitializeAsync();
        await _services.GetRequiredService<LocalizationService>().InitializeAsync();
        await _services.GetRequiredService<Metadata.ICustomProviderStore>().LoadAsync();
        await _services.GetRequiredService<IThemeService>().InitializeAsync();

        var shell = _services.GetRequiredService<MainWindow>();
        MainWindow = shell;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        shell.Show();

        _logger!.LogInformation("Main window shown.");

        if (Environment.GetEnvironmentVariable("ANIVAULT_SMOKE") == "1")
        {
            await RunNavigationSmokeTestAsync(shell);
        }
    }

    /// <summary>
    /// Dev/CI aid: with ANIVAULT_SMOKE=1, visit every sidebar page once and then exit,
    /// so a broken page or binding surfaces in the log without any clicking.
    /// </summary>
    private async Task RunNavigationSmokeTestAsync(MainWindow shell)
    {
        var viewModel = (ViewModels.ShellViewModel)shell.DataContext;
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
        var loc = _services!.GetRequiredService<Services.LocalizationService>();
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
            nav.NavigateToDetail<ViewModels.MediaDetailViewModel>(vm => vm.MediaId = media[0].Id);
            await Task.Delay(600);
            _logger!.LogInformation("Smoke: opened detail for media {Id}.", media[0].Id);
        }

        // Load-and-close each modal window so a broken window template surfaces in the log
        // (the page tour above never opens these).
        await SmokeShowWindowAsync("Add-media editor", () =>
        {
            var vm = _services!.GetRequiredService<ViewModels.MediaEditorViewModel>();
            _ = vm.InitializeForNewAsync(Models.MediaType.Anime);
            return new Views.MediaEditorWindow { DataContext = vm, Owner = shell };
        });
        await SmokeShowWindowAsync("Online search", () =>
        {
            var vm = _services!.GetRequiredService<ViewModels.OnlineSearchViewModel>();
            _ = vm.InitializeAsync(Models.MediaType.Anime);
            return new Views.OnlineSearchWindow { DataContext = vm, Owner = shell };
        });
        await SmokeShowWindowAsync("First-run", () =>
            new Views.FirstRunWindow { DataContext = _services!.GetRequiredService<ViewModels.FirstRunViewModel>(), Owner = shell });
        await SmokeShowWindowAsync("Custom provider", () =>
        {
            var vm = _services!.GetRequiredService<ViewModels.CustomProviderViewModel>();
            _ = vm.LoadAsync();
            return new Views.CustomProviderWindow { DataContext = vm, Owner = shell };
        });
        await SmokeShowWindowAsync("Rating questionnaire", () => new Views.RatingCalculatorWindow
        {
            DataContext = _services!.GetRequiredService<ViewModels.RatingCalculatorViewModel>(),
            Owner = shell,
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

    private async Task SmokeShowWindowAsync(string name, Func<System.Windows.Window> create)
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

    /// <summary>Shows the modal first-run window. Returns true when setup completed successfully.</summary>
    private bool ShowFirstRun()
    {
        var viewModel = _services!.GetRequiredService<FirstRunViewModel>();
        var window = new FirstRunWindow { DataContext = viewModel };

        var completed = false;
        viewModel.SetupCompleted += success =>
        {
            completed = success;
            window.DialogResult = success;
        };

        window.ShowDialog();
        return completed;
    }

    private static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            // EF Core is very chatty at Information; keep the log file focused on app events.
            builder.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
            builder.AddFilter("Microsoft", LogLevel.Warning);
            builder.AddDebug();
            builder.Services.AddSingleton<FileLoggerProvider>();
            builder.Services.AddSingleton<ILoggerProvider>(sp => sp.GetRequiredService<FileLoggerProvider>());
        });

        // Core infrastructure
        services.AddSingleton<IBootstrapConfigService, BootstrapConfigService>();
        services.AddSingleton<IAppPathService, AppPathService>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IMediaEditorService, MediaEditorService>();

        // Database — the connection string is resolved lazily (on first CreateDbContext), so the
        // app can start and show first-run setup before a data directory has been chosen.
        services.AddSingleton<IDbContextFactory<AppDbContext>, Data.RuntimeDbContextFactory>();
        services.AddSingleton<DatabaseInitializer>();

        // Application services
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<ISecureSettingsService, SecureSettingsService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<LocalizationService>();
        services.AddSingleton<ILocalizationService>(sp => sp.GetRequiredService<LocalizationService>());
        services.AddSingleton<IMediaService, MediaService>();
        services.AddSingleton<IMediaQueryService, MediaQueryService>();
        services.AddSingleton<ITagService, TagService>();
        services.AddSingleton<Services.Artwork.IArtworkService, Services.Artwork.ArtworkService>();
        services.AddSingleton<Services.Artwork.IImageDownloadService, Services.Artwork.ImageDownloadService>();
        services.AddSingleton<IMediaCardFactory, MediaCardFactory>();
        services.AddSingleton<Services.Backup.IBackupService, Services.Backup.BackupService>();
        services.AddSingleton<Services.Export.ILibraryExportService, Services.Export.LibraryExportService>();

        // Metadata providers (network only ever on explicit user action)
        var appVersion = typeof(App).Assembly.GetName().Version?.ToString(2) ?? "0.1";
        services.AddHttpClient(Metadata.Providers.ProviderHttp.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(20);
            client.DefaultRequestHeaders.UserAgent.ParseAdd($"AniVault/{appVersion} (personal media library)");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        });
        services.AddSingleton<Metadata.ICustomProviderStore, Metadata.CustomProviderStore>();
        services.AddSingleton<Metadata.IMetadataProvider, Metadata.Providers.BangumiProvider>();
        services.AddSingleton<Metadata.IMetadataProvider, Metadata.Providers.AniListProvider>();
        services.AddSingleton<Metadata.IMetadataProvider, Metadata.Providers.JikanProvider>();
        services.AddSingleton<Metadata.IMetadataProvider, Metadata.Providers.KitsuProvider>();
        services.AddSingleton<Metadata.IMetadataProvider, Metadata.Providers.TmdbProvider>();
        services.AddSingleton<Metadata.IMetadataProvider, Metadata.Providers.CustomMetadataProvider>();
        services.AddSingleton<Metadata.IMetadataService, Metadata.MetadataService>();
        services.AddSingleton<Metadata.IMetadataImporter, Metadata.MetadataImporter>();
        services.AddSingleton<IOnlineSearchService, OnlineSearchService>();
        services.AddSingleton<ICustomProviderService, CustomProviderService>();
        services.AddSingleton<IRatingCalculatorService, RatingCalculatorService>();

        // ViewModels
        services.AddSingleton<ShellViewModel>();
        services.AddTransient<FirstRunViewModel>();
        services.AddTransient<HomeViewModel>();
        services.AddTransient<LibraryViewModel>();
        services.AddTransient<MediaDetailViewModel>();
        services.AddTransient<TagsViewModel>();
        services.AddTransient<SeasonsViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<BackupExportViewModel>();
        services.AddTransient<MetadataSettingsViewModel>();
        services.AddTransient<CustomProviderViewModel>();
        services.AddTransient<RatingCalculatorViewModel>();
        services.AddTransient<OnlineSearchViewModel>();
        services.AddTransient<PlaceholderViewModel>();
        services.AddTransient<MediaEditorViewModel>();
        services.AddTransient<TagPickerViewModel>();
        services.AddTransient<FilterPanelViewModel>();

        // Windows
        services.AddSingleton<MainWindow>();

        return services.BuildServiceProvider();
    }

    private void SetupGlobalExceptionHandlers()
    {
        // Surface XAML data-binding failures in the log (they are otherwise silent).
        System.Diagnostics.PresentationTraceSources.Refresh();
        System.Diagnostics.PresentationTraceSources.DataBindingSource.Listeners.Add(
            new BindingErrorListener(_logger!));
        System.Diagnostics.PresentationTraceSources.DataBindingSource.Switch.Level =
            System.Diagnostics.SourceLevels.Error;

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            _logger?.LogCritical(args.ExceptionObject as Exception, "Unhandled domain exception.");
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            _logger?.LogError(args.Exception, "Unobserved task exception.");
            args.SetObserved();
        };
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _logger?.LogError(e.Exception, "Unhandled UI exception.");
        MessageBox.Show(
            "Something went wrong, but AniVault will keep running.\n"
            + "If this keeps happening, check the log file in your data folder.",
            "AniVault", MessageBoxButton.OK, MessageBoxImage.Warning);
        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _logger?.LogInformation("AniVault exiting.");
        _services?.Dispose();
        base.OnExit(e);
    }

    /// <summary>Forwards WPF binding-error trace output to the application log.</summary>
    private sealed class BindingErrorListener : System.Diagnostics.TraceListener
    {
        private readonly ILogger _logger;

        public BindingErrorListener(ILogger logger) => _logger = logger;

        public override void Write(string? message)
        {
        }

        public override void WriteLine(string? message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                _logger.LogWarning("XAML binding: {Message}", message);
            }
        }
    }
}
