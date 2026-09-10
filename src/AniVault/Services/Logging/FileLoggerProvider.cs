using System;
using System.IO;
using System.Text;
using AniVault.Services;
using Microsoft.Extensions.Logging;

namespace AniVault.Services.Logging;

/// <summary>
/// A deliberately small file logger: one line per entry, appended to
/// <c>&lt;data&gt;\Logs\app.log</c>. No external logging framework is used.
///
/// The log path is resolved lazily because logging is available before the user
/// has chosen a data directory; entries written during that window are dropped
/// (the console/debug logger still shows them to developers).
///
/// Never log API keys, personal notes or other sensitive user data.
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private const long MaxLogBytes = 2 * 1024 * 1024; // rotate at ~2 MB

    private readonly IAppPathService _paths;
    private readonly object _gate = new();

    public FileLoggerProvider(IAppPathService paths)
    {
        _paths = paths;
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    public void Dispose()
    {
    }

    /// <summary>
    /// Deletes the current and rotated log files. Safe to call while the app is running —
    /// the next entry simply recreates <c>app.log</c>. Shares the write lock so it can never
    /// race a log write.
    /// </summary>
    public void Clear()
    {
        lock (_gate)
        {
            if (!_paths.IsConfigured)
            {
                return;
            }

            foreach (var name in new[] { "app.log", "app.log.1" })
            {
                var path = Path.Combine(_paths.LogsDirectory, name);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
    }

    internal void Write(string categoryName, LogLevel level, string message, Exception? exception)
    {
        if (!_paths.IsConfigured)
        {
            return;
        }

        try
        {
            var line = new StringBuilder()
                .Append(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"))
                .Append(" [").Append(LevelLabel(level)).Append("] ")
                .Append(categoryName).Append(" - ").Append(message);

            if (exception is not null)
            {
                line.Append(Environment.NewLine).Append(exception);
            }

            lock (_gate)
            {
                Directory.CreateDirectory(_paths.LogsDirectory);
                var path = Path.Combine(_paths.LogsDirectory, "app.log");
                RotateIfNeeded(path);
                File.AppendAllText(path, line.Append(Environment.NewLine).ToString());
            }
        }
        catch
        {
            // Logging must never throw into the application.
        }
    }

    private static void RotateIfNeeded(string path)
    {
        var info = new FileInfo(path);
        if (info.Exists && info.Length > MaxLogBytes)
        {
            var archived = path + ".1";
            File.Delete(archived);
            File.Move(path, archived);
        }
    }

    private static string LevelLabel(LogLevel level) => level switch
    {
        LogLevel.Trace => "TRC",
        LogLevel.Debug => "DBG",
        LogLevel.Information => "INF",
        LogLevel.Warning => "WRN",
        LogLevel.Error => "ERR",
        LogLevel.Critical => "CRT",
        _ => "???",
    };

    private sealed class FileLogger : ILogger
    {
        private readonly FileLoggerProvider _provider;
        private readonly string _category;

        public FileLogger(FileLoggerProvider provider, string category)
        {
            _provider = provider;
            _category = category;
        }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            _provider.Write(_category, logLevel, formatter(state, exception), exception);
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
