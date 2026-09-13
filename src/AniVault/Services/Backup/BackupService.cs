using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AniVault.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AniVault.Services.Backup;

/// <summary>
/// Creates and restores ZIP backups of the whole local library (database + artwork + manifest).
/// Everything is offline. Restore replaces the current library, so the caller must warn the user
/// and arrange an application restart afterwards.
/// </summary>
public interface IBackupService
{
    /// <summary>
    /// Writes a backup archive into <paramref name="destinationDirectory"/> and returns its full
    /// path. <paramref name="progress"/>, if given, is reported a 0.0-1.0 fraction as each file is
    /// added to the archive — a large library's artwork folder is what actually takes visible time.
    /// </summary>
    Task<string> CreateBackupAsync(
        string destinationDirectory, IProgress<double>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>Validates an archive without changing anything.</summary>
    Task<BackupInspection> InspectAsync(string backupArchivePath, CancellationToken cancellationToken = default);

    /// <summary>Replaces the current library with the contents of the archive. Restart required afterwards.</summary>
    Task RestoreAsync(string backupArchivePath, CancellationToken cancellationToken = default);
}

public sealed class BackupService : IBackupService
{
    private const string ManifestEntryName = "backup-manifest.json";

    /// <summary>Artwork folders inside the data directory that a backup captures verbatim.
    /// The database is captured separately via a consistent snapshot (see below).</summary>
    private static readonly string[] ArtworkFolders = { "Anime", "Movies", "TV" };

    /// <summary>All top-level folders a restore replaces.</summary>
    private static readonly string[] RestoredFolders = { "Database", "Anime", "Movies", "TV" };

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly IAppPathService _paths;
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly ILogger<BackupService> _logger;

    public BackupService(
        IAppPathService paths,
        IDbContextFactory<AppDbContext> contextFactory,
        ILogger<BackupService> logger)
    {
        _paths = paths;
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task<string> CreateBackupAsync(
        string destinationDirectory, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        if (!_paths.IsConfigured)
        {
            throw new InvalidOperationException("The data directory is not configured.");
        }

        Directory.CreateDirectory(destinationDirectory);

        // A previous backup that was killed mid-write (crash, forced exit, power loss) leaves a
        // ".tmp" behind — it never got renamed to a real ".zip", so nothing ever mistook it for a
        // finished backup, but it's still disk clutter. Sweep it before starting a new one.
        CleanupStaleTempFiles(destinationDirectory);

        var manifest = await BuildManifestAsync(cancellationToken);
        var fileName = $"AniVault_Backup_{DateTime.Now:yyyy-MM-dd_HHmmss}.zip";
        var finalPath = Path.Combine(destinationDirectory, fileName);
        var tempPath = finalPath + ".tmp";

        // A consistent point-in-time copy of the live database (no file locking issues).
        var snapshotPath = await SnapshotDatabaseAsync(cancellationToken);

        // Count ahead of time so progress is a real fraction of the work, not a guess. Artwork
        // files dominate the total for any library with more than a handful of items.
        var artworkFiles = ArtworkFolders
            .Select(folder => Path.Combine(_paths.DataDirectory!, folder))
            .Where(Directory.Exists)
            .SelectMany(dir => Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            .ToList();
        var totalSteps = 2 + artworkFiles.Count; // manifest entry + db snapshot entry + each artwork file
        var completedSteps = 0;
        void ReportStep() => progress?.Report(Math.Min(1.0, (double)++completedSteps / totalSteps));

        try
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            await using (var stream = new FileStream(tempPath, FileMode.CreateNew))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                var manifestEntry = archive.CreateEntry(ManifestEntryName, CompressionLevel.Optimal);
                await using (var entryStream = manifestEntry.Open())
                {
                    await JsonSerializer.SerializeAsync(entryStream, manifest, JsonOptions, cancellationToken);
                }

                ReportStep();

                archive.CreateEntryFromFile(snapshotPath, "Database/AniVault.db", CompressionLevel.Optimal);
                ReportStep();

                foreach (var folder in ArtworkFolders)
                {
                    AddFolderToArchive(archive, Path.Combine(_paths.DataDirectory!, folder), folder, cancellationToken, ReportStep);
                }
            }

            if (File.Exists(finalPath))
            {
                File.Delete(finalPath);
            }

            File.Move(tempPath, finalPath);
            _logger.LogInformation("Created backup {File} ({MediaCount} media).", fileName, manifest.MediaCount);
            return finalPath;
        }
        finally
        {
            TryDeleteFile(tempPath);
            TryDeleteFile(snapshotPath);
        }
    }

    /// <summary>Removes any orphaned "*.zip.tmp" left in <paramref name="destinationDirectory"/> by a backup that never finished.</summary>
    private static void CleanupStaleTempFiles(string destinationDirectory)
    {
        if (!Directory.Exists(destinationDirectory))
        {
            return;
        }

        foreach (var stale in Directory.EnumerateFiles(destinationDirectory, "AniVault_Backup_*.zip.tmp"))
        {
            TryDeleteFile(stale);
        }
    }

    /// <summary>Uses SQLite's <c>VACUUM INTO</c> to write a clean, consistent copy of the database.</summary>
    private async Task<string> SnapshotDatabaseAsync(CancellationToken cancellationToken)
    {
        var snapshotDir = Path.Combine(_paths.CacheDirectory, "backup-snapshot");
        Directory.CreateDirectory(snapshotDir);
        var snapshotPath = Path.Combine(snapshotDir, "AniVault.db");
        TryDeleteFile(snapshotPath);

        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);

        // VACUUM INTO requires a string literal for the target — it cannot be parameterised.
        // The path is app-controlled (our own cache directory) and single quotes are escaped,
        // so this is safe despite the EF1002 analyzer warning.
        var escaped = snapshotPath.Replace("'", "''");
        var sql = "VACUUM INTO '" + escaped + "';";
        await db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
        return snapshotPath;
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
    }

    public async Task<BackupInspection> InspectAsync(string backupArchivePath, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var stream = File.OpenRead(backupArchivePath);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

            var manifestEntry = archive.GetEntry(ManifestEntryName);
            if (manifestEntry is null)
            {
                return new BackupInspection(false, "This archive has no AniVault manifest.", null, Path.GetFileName(backupArchivePath));
            }

            BackupManifest? manifest;
            await using (var entryStream = manifestEntry.Open())
            {
                manifest = await JsonSerializer.DeserializeAsync<BackupManifest>(entryStream, cancellationToken: cancellationToken);
            }

            if (manifest is null || manifest.Signature != "AniVault.Backup")
            {
                return new BackupInspection(false, "This does not look like an AniVault backup.", null, Path.GetFileName(backupArchivePath));
            }

            if (archive.GetEntry("Database/AniVault.db") is null)
            {
                return new BackupInspection(false, "The archive is missing its database file.", manifest, Path.GetFileName(backupArchivePath));
            }

            return new BackupInspection(true, null, manifest, Path.GetFileName(backupArchivePath));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to inspect backup archive {Path}.", backupArchivePath);
            return new BackupInspection(false, "The file could not be read as a ZIP archive.", null, Path.GetFileName(backupArchivePath));
        }
    }

    public async Task RestoreAsync(string backupArchivePath, CancellationToken cancellationToken = default)
    {
        var inspection = await InspectAsync(backupArchivePath, cancellationToken);
        if (!inspection.IsValid)
        {
            throw new InvalidOperationException(inspection.Error ?? "The backup archive is not valid.");
        }

        // Release every pooled SQLite handle before we replace the database file.
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        var stagingDirectory = Path.Combine(_paths.CacheDirectory, $"restore-{DateTime.Now:yyyyMMdd-HHmmss}");
        Directory.CreateDirectory(stagingDirectory);

        try
        {
            ZipFile.ExtractToDirectory(backupArchivePath, stagingDirectory, overwriteFiles: true);

            foreach (var folder in RestoredFolders)
            {
                var source = Path.Combine(stagingDirectory, folder);
                var target = Path.Combine(_paths.DataDirectory!, folder);

                if (Directory.Exists(target))
                {
                    Directory.Delete(target, recursive: true);
                }

                if (Directory.Exists(source))
                {
                    Directory.Move(source, target);
                }
                else
                {
                    Directory.CreateDirectory(target);
                }
            }

            // Drop any stale WAL side-files so the restored database is the only truth.
            foreach (var side in new[] { "AniVault.db-wal", "AniVault.db-shm" })
            {
                TryDeleteFile(Path.Combine(_paths.DatabaseDirectory, side));
            }

            _logger.LogInformation("Restored library from backup {File}.", inspection.FileName);
        }
        finally
        {
            if (Directory.Exists(stagingDirectory))
            {
                Directory.Delete(stagingDirectory, recursive: true);
            }
        }
    }

    private async Task<BackupManifest> BuildManifestAsync(CancellationToken cancellationToken)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return new BackupManifest
        {
            AppVersion = typeof(BackupService).Assembly.GetName().Version?.ToString() ?? "0.0.0",
            CreatedAt = DateTimeOffset.Now,
            MediaCount = await db.Media.CountAsync(cancellationToken),
            TagCount = await db.Tags.CountAsync(cancellationToken),
        };
    }

    private static void AddFolderToArchive(
        ZipArchive archive, string sourceFolder, string entryPrefix, CancellationToken cancellationToken, Action? onFileAdded = null)
    {
        if (!Directory.Exists(sourceFolder))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(sourceFolder, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relative = Path.GetRelativePath(sourceFolder, file).Replace('\\', '/');
            archive.CreateEntryFromFile(file, $"{entryPrefix}/{relative}", CompressionLevel.Optimal);
            onFileAdded?.Invoke();
        }
    }
}
