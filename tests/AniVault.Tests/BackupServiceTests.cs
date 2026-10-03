using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AniVault.Data;
using AniVault.Models;
using AniVault.Services.Backup;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
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
    public async Task A_Backup_Made_Before_The_Latest_Migration_Restores_And_Upgrades_With_Nothing_Lost()
    {
        // The schema AniVault 1.8.0 shipped: everything up to AddTagSortOrder, no EpisodeListHidden.
        const string previousSchema = "20260911145403_AddTagSortOrder";

        var workDir = Path.Combine(Path.GetTempPath(), $"anivault-oldbackup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workDir);
        var oldDbPath = Path.Combine(workDir, "AniVault.db");
        var archivePath = Path.Combine(workDir, "AniVault_Backup_old.zip");

        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={oldDbPath}").Options;
            await using (var old = new AppDbContext(options))
            {
                await old.GetService<IMigrator>().MigrateAsync(previousSchema);

                // Raw SQL on purpose: the current EF model already knows the new column, so going
                // through it would not be an honest stand-in for a database an older build wrote.
                await old.Database.ExecuteSqlRawAsync(
                    """
                    INSERT INTO Media (Id, MediaType, Title, Status, MyRating, IsFavorite, IsLiked, ShowOnHome, Notes,
                                       EpisodeCount, PosterPath, CreatedAt, UpdatedAt)
                    VALUES (1, 0, 'Frieren', 2, 9.5, 1, 1, 0, 'rewatch someday',
                            2, 'Anime/1/poster.jpg', '2026-01-01 00:00:00', '2026-01-02 00:00:00');
                    INSERT INTO Episodes (MediaId, EpisodeNumber, IsWatched) VALUES (1, 1, 1), (1, 2, 0);
                    INSERT INTO Tags (Id, Name, NormalizedName, SortOrder) VALUES (1, 'Fantasy', 'fantasy', 0);
                    INSERT INTO MediaTags (MediaId, TagId) VALUES (1, 1);
                    """);
            }

            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            using (var archive = System.IO.Compression.ZipFile.Open(archivePath, System.IO.Compression.ZipArchiveMode.Create))
            {
                var manifest = archive.CreateEntry("backup-manifest.json");
                await using (var writer = new StreamWriter(manifest.Open()))
                {
                    await writer.WriteAsync(
                        """{"FormatVersion":1,"AppVersion":"1.8.0.0","CreatedAt":"2026-09-18T23:53:47+08:00","MediaCount":1,"TagCount":1,"Signature":"AniVault.Backup"}""");
                }

                System.IO.Compression.ZipFileExtensions.CreateEntryFromFile(archive, oldDbPath, "Database/AniVault.db");
                var poster = archive.CreateEntry("Anime/1/poster.jpg");
                await using (var writer = new StreamWriter(poster.Open()))
                {
                    await writer.WriteAsync("not-a-real-image");
                }
            }

            using var library = new TestLibrary();
            var service = new BackupService(library.Paths, library, NullLogger<BackupService>.Instance);

            var inspection = await service.InspectAsync(archivePath);
            Assert.True(inspection.IsValid);

            await service.RestoreAsync(archivePath);

            // What the app does on its next start after a restore.
            await new DatabaseInitializer(library, NullLogger<DatabaseInitializer>.Instance).InitializeAsync();

            await using var db = library.CreateDbContext();
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());

            var media = await db.Media
                .Include(m => m.Episodes)
                .Include(m => m.MediaTags).ThenInclude(mt => mt.Tag)
                .SingleAsync();

            Assert.Equal("Frieren", media.Title);
            Assert.Equal(WatchStatus.Completed, media.Status);
            Assert.Equal(9.5, media.MyRating);
            Assert.True(media.IsFavorite);
            Assert.True(media.IsLiked);
            Assert.False(media.ShowOnHome);
            Assert.Equal("rewatch someday", media.Notes);
            Assert.False(media.EpisodeListHidden); // the new column: existing lists stay visible
            Assert.Equal(new[] { true, false }, media.Episodes.OrderBy(e => e.EpisodeNumber).Select(e => e.IsWatched));
            Assert.Equal("Fantasy", media.MediaTags.Single().Tag!.Name);
            Assert.True(File.Exists(library.Paths.ToAbsolutePath(media.PosterPath!)));
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(workDir))
            {
                Directory.Delete(workDir, recursive: true);
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
