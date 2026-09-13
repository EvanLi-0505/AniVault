using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AniVault.Models;
using AniVault.Services.Backup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AniVault.Tests;

public class BackupServiceTests
{
    [Fact]
    public async Task Backup_Then_Restore_Round_Trips_Database_And_Artwork()
    {
        using var library = new TestLibrary();
        var service = new BackupService(library.Paths, library, NullLogger<BackupService>.Instance);

        // Seed a media row and a poster file inside the data directory.
        await using (var db = library.CreateDbContext())
        {
            db.Media.Add(new Media { MediaType = MediaType.Anime, Title = "Frieren", Status = WatchStatus.Completed });
            await db.SaveChangesAsync();
        }

        var posterDir = library.Paths.GetMediaAssetDirectory(MediaType.Anime, 1);
        Directory.CreateDirectory(posterDir);
        var posterPath = Path.Combine(posterDir, "poster.jpg");
        await File.WriteAllTextAsync(posterPath, "not-a-real-image");

        var backupDir = Path.Combine(Path.GetTempPath(), $"anivault-backup-{System.Guid.NewGuid():N}");
        try
        {
            var archivePath = await service.CreateBackupAsync(backupDir);
            Assert.True(File.Exists(archivePath));

            var inspection = await service.InspectAsync(archivePath);
            Assert.True(inspection.IsValid);
            Assert.Equal(1, inspection.Manifest!.MediaCount);

            // Wipe the live library, then restore.
            await using (var db = library.CreateDbContext())
            {
                db.Media.RemoveRange(db.Media);
                await db.SaveChangesAsync();
            }

            File.Delete(posterPath);

            await service.RestoreAsync(archivePath);

            await using (var db = library.CreateDbContext())
            {
                var titles = await db.Media.Select(m => m.Title).ToListAsync();
                Assert.Equal(new[] { "Frieren" }, titles);
            }

            Assert.True(File.Exists(posterPath));
        }
        finally
        {
            if (Directory.Exists(backupDir))
            {
                Directory.Delete(backupDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CreateBackup_Reports_Progress_From_Zero_Towards_One()
    {
        using var library = new TestLibrary();
        var service = new BackupService(library.Paths, library, NullLogger<BackupService>.Instance);

        await using (var db = library.CreateDbContext())
        {
            db.Media.Add(new Media { MediaType = MediaType.Anime, Title = "A" });
            await db.SaveChangesAsync();
        }

        var posterDir = library.Paths.GetMediaAssetDirectory(MediaType.Anime, 1);
        Directory.CreateDirectory(posterDir);
        await File.WriteAllTextAsync(Path.Combine(posterDir, "poster.jpg"), "not-a-real-image");
        await File.WriteAllTextAsync(Path.Combine(posterDir, "backdrop.jpg"), "not-a-real-image-either");

        var backupDir = Path.Combine(Path.GetTempPath(), $"anivault-backup-{System.Guid.NewGuid():N}");
        var reported = new System.Collections.Generic.List<double>();
        var progress = new Progress<double>(reported.Add);

        try
        {
            await service.CreateBackupAsync(backupDir, progress);

            // Progress<T> marshals via the SynchronizationContext captured at construction; in a
            // test there is none, so callbacks run synchronously and are all visible immediately.
            Assert.NotEmpty(reported);
            Assert.True(reported[^1] >= 0.99);
            Assert.All(reported, f => Assert.InRange(f, 0.0, 1.0));
        }
        finally
        {
            if (Directory.Exists(backupDir))
            {
                Directory.Delete(backupDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CreateBackup_Sweeps_A_Stale_Tmp_File_From_A_Previous_Interrupted_Backup()
    {
        using var library = new TestLibrary();
        var service = new BackupService(library.Paths, library, NullLogger<BackupService>.Instance);

        var backupDir = Path.Combine(Path.GetTempPath(), $"anivault-backup-{System.Guid.NewGuid():N}");
        Directory.CreateDirectory(backupDir);
        var staleTemp = Path.Combine(backupDir, "AniVault_Backup_2020-01-01_000000.zip.tmp");
        await File.WriteAllTextAsync(staleTemp, "leftover from a killed process");

        try
        {
            await service.CreateBackupAsync(backupDir);

            Assert.False(File.Exists(staleTemp));
        }
        finally
        {
            if (Directory.Exists(backupDir))
            {
                Directory.Delete(backupDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Inspect_Rejects_A_Non_Backup_Zip()
    {
        using var library = new TestLibrary();
        var service = new BackupService(library.Paths, library, NullLogger<BackupService>.Instance);

        var strayZipDir = Path.Combine(Path.GetTempPath(), $"stray-{System.Guid.NewGuid():N}");
        Directory.CreateDirectory(strayZipDir);
        await File.WriteAllTextAsync(Path.Combine(strayZipDir, "readme.txt"), "hello");
        var zipPath = strayZipDir + ".zip";
        System.IO.Compression.ZipFile.CreateFromDirectory(strayZipDir, zipPath);

        try
        {
            var inspection = await service.InspectAsync(zipPath);
            Assert.False(inspection.IsValid);
            Assert.NotNull(inspection.Error);
        }
        finally
        {
            Directory.Delete(strayZipDir, recursive: true);
            File.Delete(zipPath);
        }
    }
}
