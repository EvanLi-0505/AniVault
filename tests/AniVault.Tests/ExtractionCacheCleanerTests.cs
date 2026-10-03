using System;
using System.IO;
using AniVault.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace AniVault.Tests;

public class ExtractionCacheCleanerTests
{
    [Fact]
    public void Removes_Other_Builds_But_Keeps_The_Current_One_And_Anything_Still_In_Use()
    {
        var root = Path.Combine(Path.GetTempPath(), $"anivault-extract-{Guid.NewGuid():N}");
        var appFolder = Path.Combine(root, ".net", "AniVault");
        var current = Directory.CreateDirectory(Path.Combine(appFolder, "current")).FullName;
        var old = Directory.CreateDirectory(Path.Combine(appFolder, "old")).FullName;
        var inUse = Directory.CreateDirectory(Path.Combine(appFolder, "inuse")).FullName;
        var halfDeleted = Directory.CreateDirectory(Path.Combine(appFolder, "earlier.delete")).FullName;

        File.WriteAllText(Path.Combine(current, "e_sqlite3.dll"), "current");
        File.WriteAllText(Path.Combine(old, "e_sqlite3.dll"), "old");
        File.WriteAllText(Path.Combine(old, "wpfgfx_cor3.dll"), "old");
        File.WriteAllText(Path.Combine(inUse, "PenImc_cor3.dll"), "not loaded yet");
        File.WriteAllText(Path.Combine(halfDeleted, "leftover.dll"), "from an interrupted clean-up");
        var lockedPath = Path.Combine(inUse, "e_sqlite3.dll");
        File.WriteAllText(lockedPath, "loaded by another running copy");

        try
        {
            int removed;
            using (new FileStream(lockedPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                removed = ExtractionCacheCleaner.RemoveOtherBuilds(current, NullLogger.Instance);
            }

            Assert.Equal(2, removed);
            Assert.False(Directory.Exists(old));
            Assert.False(Directory.Exists(halfDeleted));
            Assert.True(File.Exists(Path.Combine(current, "e_sqlite3.dll")));

            // The folder another copy is using is left whole — including the file it has not loaded yet.
            Assert.True(File.Exists(lockedPath));
            Assert.True(File.Exists(Path.Combine(inUse, "PenImc_cor3.dll")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void A_Dev_Or_Test_Run_Has_No_Extraction_Folder_So_Nothing_Is_Cleaned()
        => Assert.Null(ExtractionCacheCleaner.FindCurrentExtractionDirectory());
}
