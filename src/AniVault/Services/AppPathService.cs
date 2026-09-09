using System;
using System.IO;

namespace AniVault.Services;

/// <summary>
/// Resolves every filesystem location the application uses, all rooted at the
/// user-selected data directory. Also converts between absolute paths and the
/// data-directory-relative paths stored in the database (which keeps the library portable).
/// </summary>
public interface IAppPathService
{
    /// <summary>True once a valid, existing data directory has been configured.</summary>
    bool IsConfigured { get; }

    /// <summary>Absolute path to the data directory, or null when not configured.</summary>
    string? DataDirectory { get; }

    string DatabaseDirectory { get; }

    string DatabaseFilePath { get; }

    string LogsDirectory { get; }

    string BackupsDirectory { get; }

    string CacheDirectory { get; }

    /// <summary>Re-reads the bootstrap config (call after first-run setup writes it).</summary>
    void Reload();

    /// <summary>Points the service at a directory and persists the choice. Does not create folders.</summary>
    void SetDataDirectory(string absolutePath);

    /// <summary>Creates the standard sub-folder layout inside the data directory.</summary>
    void EnsureDirectoryStructure();

    /// <summary>Folder that holds artwork for a given media item, e.g. &lt;data&gt;\Anime\12.</summary>
    string GetMediaAssetDirectory(Models.MediaType mediaType, int mediaId);

    /// <summary>Converts an absolute path inside the data directory to a stored relative path.</summary>
    string ToRelativePath(string absolutePath);

    /// <summary>Converts a stored relative path back to an absolute path.</summary>
    string ToAbsolutePath(string relativePath);
}

/// <inheritdoc cref="IAppPathService" />
public sealed class AppPathService : IAppPathService
{
    private readonly IBootstrapConfigService _bootstrap;
    private string? _dataDirectory;

    public AppPathService(IBootstrapConfigService bootstrap)
    {
        _bootstrap = bootstrap;
        Reload();
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_dataDirectory) && Directory.Exists(_dataDirectory);

    public string? DataDirectory => _dataDirectory;

    public string DatabaseDirectory => Combine("Database");

    public string DatabaseFilePath => Path.Combine(DatabaseDirectory, "AniVault.db");

    public string LogsDirectory => Combine("Logs");

    public string BackupsDirectory => Combine("Backups");

    public string CacheDirectory => Combine("Cache");

    public void Reload()
    {
        _dataDirectory = _bootstrap.Load().DataDirectory;
    }

    public void SetDataDirectory(string absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath))
        {
            throw new ArgumentException("Data directory path must not be empty.", nameof(absolutePath));
        }

        _dataDirectory = Path.GetFullPath(absolutePath);

        var config = _bootstrap.Load();
        config.DataDirectory = _dataDirectory;
        _bootstrap.Save(config);
    }

    public void EnsureDirectoryStructure()
    {
        RequireConfigured();
        Directory.CreateDirectory(_dataDirectory!);
        Directory.CreateDirectory(DatabaseDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(BackupsDirectory);
        Directory.CreateDirectory(CacheDirectory);
        Directory.CreateDirectory(Combine("Anime"));
        Directory.CreateDirectory(Combine("Movies"));
        Directory.CreateDirectory(Combine("TV"));
    }

    public string GetMediaAssetDirectory(Models.MediaType mediaType, int mediaId)
    {
        var typeFolder = mediaType switch
        {
            Models.MediaType.Anime => "Anime",
            Models.MediaType.Movie => "Movies",
            Models.MediaType.TvSeries => "TV",
            _ => "Other",
        };
        return Combine(typeFolder, mediaId.ToString());
    }

    public string ToRelativePath(string absolutePath)
    {
        RequireConfigured();
        return Path.GetRelativePath(_dataDirectory!, absolutePath)
            .Replace('\\', '/');
    }

    public string ToAbsolutePath(string relativePath)
    {
        RequireConfigured();
        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
        return Path.GetFullPath(Path.Combine(_dataDirectory!, normalized));
    }

    private string Combine(params string[] parts)
    {
        RequireConfigured();
        var all = new string[parts.Length + 1];
        all[0] = _dataDirectory!;
        Array.Copy(parts, 0, all, 1, parts.Length);
        return Path.Combine(all);
    }

    private void RequireConfigured()
    {
        if (string.IsNullOrWhiteSpace(_dataDirectory))
        {
            throw new InvalidOperationException("The data directory has not been configured yet.");
        }
    }
}
