using System;
using System.Threading.Tasks;
using System.Windows;
using AniVault.Composition;
using AniVault.Data;
using AniVault.Services;
using AniVault.Services.Logging;
using AniVault.ViewModels;
using AniVault.Views;
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
    private SingleInstanceGuard? _instanceGuard;

    private int _recentUiExceptions;
    private DateTimeOffset _lastUiException;
    private DateTimeOffset _lastCrashDialog;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Keep the process alive across the first-run window closing before the main window opens.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // One process only. A second launch just brings the running window to the front.
        // (Skipped under the smoke test so sequential CI runs don't fight over the mutex.)
        if (Environment.GetEnvironmentVariable("ANIVAULT_SMOKE") != "1")
        {
            _instanceGuard = new SingleInstanceGuard("AniVault");
            if (!_instanceGuard.IsPrimary)
            {
                _instanceGuard.SignalPrimaryInstance();
                _instanceGuard.Dispose();
                _instanceGuard = null;
                Shutdown();
                return;
            }

            _instanceGuard.ActivationRequested += () => Dispatcher.BeginInvoke(BringToFront);
        }

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

    /// <summary>
    /// The composition root. Each subsystem registers itself in its own extension method
    /// (<see cref="Composition.ServiceCollectionExtensions"/>) so adding a new one — the
    /// intended shape for a future feature like online watching — is a single line here,
    /// not more entries dropped into a long flat list. See docs/ARCHITECTURE.md →
    /// "Adding a pluggable subsystem".
    /// </summary>
    private static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        var appVersion = typeof(App).Assembly.GetName().Version?.ToString(2) ?? "0.1";

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

        services.AddCoreInfrastructure();
        services.AddDatabase();
        services.AddDomainServices();
        services.AddMetadataServices(appVersion);
        services.AddFeatureServices();
        services.AddViewModels();

        return services.BuildServiceProvider();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _logger?.LogInformation("AniVault exiting.");
        _instanceGuard?.Dispose();
        _services?.Dispose();
        base.OnExit(e);
    }
}
