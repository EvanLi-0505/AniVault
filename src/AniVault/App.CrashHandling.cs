using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using AniVault.Services;
using Microsoft.Extensions.Logging;

namespace AniVault;

/// <summary>
/// Global exception handling and single-instance activation: log everything, never let a UI
/// exception actually crash the process, rate-limit the "kept running" notice, and offer a
/// clean restart if failures keep coming. Kept separate from the startup path in
/// <c>App.xaml.cs</c> so the two concerns (booting vs. staying alive) don't tangle.
/// </summary>
public partial class App
{
    private void SetupGlobalExceptionHandlers()
    {
        // Surface XAML data-binding failures in the log (they are otherwise silent).
        PresentationTraceSources.Refresh();
        PresentationTraceSources.DataBindingSource.Listeners.Add(new BindingErrorListener(_logger!));
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;

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
        e.Handled = true;
        _logger?.LogError(e.Exception, "Unhandled UI exception.");

        var now = DateTimeOffset.UtcNow;
        _recentUiExceptions = now - _lastUiException > TimeSpan.FromMinutes(2) ? 1 : _recentUiExceptions + 1;
        _lastUiException = now;

        // A burst of failures means the app is probably wedged — offer a clean restart.
        if (_recentUiExceptions >= 5)
        {
            _recentUiExceptions = 0;
            if (MessageBox.Show(Loc("Crash.RestartPrompt"), "AniVault",
                    MessageBoxButton.YesNo, MessageBoxImage.Error) == MessageBoxResult.Yes)
            {
                RestartSelf();
            }

            return;
        }

        // Otherwise: a quiet "kept running" note, rate-limited so a repeating fault can't spam it.
        if (now - _lastCrashDialog > TimeSpan.FromSeconds(5))
        {
            _lastCrashDialog = now;
            MessageBox.Show(Loc("Crash.Continue"), "AniVault", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void RestartSelf()
    {
        try
        {
            // Release the single-instance mutex first, or the fresh process just bounces off it.
            _instanceGuard?.Dispose();
            _instanceGuard = null;

            var exe = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exe))
            {
                Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Could not relaunch AniVault.");
        }
        finally
        {
            Shutdown();
        }
    }

    /// <summary>Called when a second launch signals this (primary) instance to come forward.</summary>
    private void BringToFront()
    {
        if (MainWindow is not { } window)
        {
            return;
        }

        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.Activate();
        window.Topmost = true;
        window.Topmost = false;
    }

    private static string Loc(string key) => LocalizationService.Instance?.Text(key) ?? key;

    /// <summary>Forwards WPF binding-error trace output to the application log.</summary>
    private sealed class BindingErrorListener : TraceListener
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
