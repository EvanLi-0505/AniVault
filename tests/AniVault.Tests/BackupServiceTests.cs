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
